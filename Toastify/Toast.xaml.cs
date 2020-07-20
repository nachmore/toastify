﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.IO;
using System.Diagnostics;
using Toastify.Plugin;
using System.Threading.Tasks;

namespace Toastify
{
  public partial class Toast : Window
  {
    private const string DEFAULT_ICON = "SpotifyToastifyLogo.png";
    private const string AD_PLAYING_ICON = "SpotifyAdPlaying.png";
    private const string ALBUM_ACCESS_DENIED_ICON = "ToastifyAccessDenied.png";

    Timer watchTimer;
    Timer minimizeTimer;

    System.Windows.Forms.NotifyIcon trayIcon;

    /// <summary>
    /// Holds the actual icon shown on the toast
    /// </summary>
    string toastIcon = "";

    BitmapImage cover;

    private VersionChecker versionChecker;
    private bool isUpdateToast = false;

    internal List<IPluginBase> Plugins { get; set; }

    internal static Toast Current { get; private set; }

    /// <summary>
    /// To the best of our knowledge this is our current playing song
    /// </summary>
    Song currentSong = null;

    private bool dragging = false;

    public new Visibility Visibility
    {
      get { return base.Visibility; }
      set
      {
        base.Visibility = value;
      }
    }

    public void LoadSettings()
    {

      try
      {
        SettingsXml.Current.Load();
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine("Exception loading settings:\n" + ex);

        MessageBox.Show(@"Toastify was unable to load the settings file." + Environment.NewLine +
                            "Delete the Toastify.xml file and restart the application to recreate the settings file." + Environment.NewLine +
                        Environment.NewLine +
                        "The application will now be started with default settings.", "Toastify", MessageBoxButton.OK, MessageBoxImage.Information);

        SettingsXml.Current.Default(setHotKeys: true);
      }
    }

    public Toast()
    {
      InitializeComponent();

      // set a static reference back to ourselves, useful for callbacks
      Current = this;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      //Load settings from XML
      LoadSettings();

      string version = VersionChecker.Version;

      Telemetry.TrackEvent(TelemetryCategory.General, Telemetry.TelemetryEvent.AppLaunch, version);

      if (SettingsXml.Current.PreviousOS != version)
      {
        Telemetry.TrackEvent(TelemetryCategory.General, Telemetry.TelemetryEvent.AppUpgraded, version);

        SettingsXml.Current.PreviousOS = version;
      }

      //Init toast(color settings)
      InitToast();

      //Init tray icon
      trayIcon = new System.Windows.Forms.NotifyIcon
      {
        Icon = Toastify.Properties.Resources.spotifyicon,
        Text = "Toastify",
        Visible = true,

        ContextMenu = new System.Windows.Forms.ContextMenu()
      };

      //Init tray icon menu
      System.Windows.Forms.MenuItem menuSettings = new System.Windows.Forms.MenuItem
      {
        Text = "Settings"
      };
      menuSettings.Click += (s, ev) => { Settings.Launch(this); };

      trayIcon.ContextMenu.MenuItems.Add(menuSettings);

      System.Windows.Forms.MenuItem menuAbout = new System.Windows.Forms.MenuItem
      {
        Text = "About Toastify..."
      };
      menuAbout.Click += (s, ev) => { new About().ShowDialog(); };

      trayIcon.ContextMenu.MenuItems.Add(menuAbout);

      trayIcon.ContextMenu.MenuItems.Add("-");

      System.Windows.Forms.MenuItem menuExit = new System.Windows.Forms.MenuItem
      {
        Text = "Exit"
      };
      menuExit.Click += (s, ev) => { Application.Current.Shutdown(); }; //this.Close(); };

      trayIcon.ContextMenu.MenuItems.Add(menuExit);

      trayIcon.MouseClick += (s, ev) => { if (ev.Button == System.Windows.Forms.MouseButtons.Left) DisplayAction(SpotifyAction.ShowToast, null); };

      trayIcon.DoubleClick += (s, ev) => { Settings.Launch(this); };

      //Init watch timer
      watchTimer = new Timer(1000);
      watchTimer.Elapsed += async (s, ev) =>
      {
        watchTimer.Stop();
        await CheckTitle();
        watchTimer.Start();
      };

      this.Deactivated += Toast_Deactivated;

      //Remove from ALT+TAB
      WinHelper.AddToolWindowStyle(this);

      //Check if Spotify is running.
      AskUserToStartSpotify();
      LoadPlugins();

      //Let the plugins know we're started.
      foreach (var p in this.Plugins)
      {
        try
        {
          p.Started();
        }
        catch (Exception)
        {
          //For now we swallow any plugin errors.
        }
      }

      if (!SettingsXml.Current.DisableToast)
        watchTimer.Enabled = true; //Only need to be enabled if we are going to show the toast.

      versionChecker = new VersionChecker();
      versionChecker.CheckVersionComplete += new EventHandler<CheckVersionCompleteEventArgs>(VersionChecker_CheckVersionComplete);
      versionChecker.BeginCheckVersion();

      // TODO: right now this is pretty dumb - kick off update notifications every X hours, this might get annoying
      //       and really we should just pop a notification once per version and probably immediately after a song toast
      var updateTimer = new System.Windows.Threading.DispatcherTimer();
      updateTimer.Tick += (timerSender, timerE) => { versionChecker.BeginCheckVersion(); };
      updateTimer.Interval = new TimeSpan(6, 0, 0);
      updateTimer.Start();
    }

    void Toast_Deactivated(object sender, EventArgs e)
    {
      this.Topmost = true;
    }

    public void InitToast()
    {
      const double MIN_WIDTH = 200.0;
      const double MIN_HEIGHT = 65.0;

      //If we find any invalid settings in the xml we skip it and use default.
      //User notification of bad settings will be implemented with the settings dialog.

      //This method is UGLY but we'll keep it until the settings dialog is implemented.
      SettingsXml settings = SettingsXml.Current;

      ToastBorder.BorderThickness = new Thickness(settings.ToastBorderThickness);

      ColorConverter cc = new ColorConverter();
      if (!string.IsNullOrEmpty(settings.ToastBorderColor) && cc.IsValid(settings.ToastBorderColor))
        ToastBorder.BorderBrush = new SolidColorBrush((Color)cc.ConvertFrom(settings.ToastBorderColor));

      if (!string.IsNullOrEmpty(settings.ToastColorTop) && !string.IsNullOrEmpty(settings.ToastColorBottom) && cc.IsValid(settings.ToastColorTop) && cc.IsValid(settings.ToastColorBottom))
      {
        Color top = (Color)cc.ConvertFrom(settings.ToastColorTop);
        Color botton = (Color)cc.ConvertFrom(settings.ToastColorBottom);

        ToastBorder.Background = new LinearGradientBrush(top, botton, 90.0);
      }

      if (settings.ToastWidth >= MIN_WIDTH)
        this.Width = settings.ToastWidth;
      if (settings.ToastHeight >= MIN_HEIGHT)
        this.Height = settings.ToastHeight;

      //If we made it this far we have all the values needed.
      ToastBorder.CornerRadius = new CornerRadius(settings.ToastBorderCornerRadiusTopLeft, settings.ToastBorderCornerRadiusTopRight, settings.ToastBorderCornerRadiusBottomRight, settings.ToastBorderCornerRadiusBottomLeft);
    }

    private async Task CheckTitle()
    {
      Song currentSong = Spotify.GetCurrentSong();

      if (currentSong != null && currentSong.IsValid() && !currentSong.Equals(this.currentSong))
      {
        // set the previous title asap so that the next timer call to this function will
        // fail fast (setting it at the end may cause multiple web requests)
        this.currentSong = currentSong;

        try
        {
          await Spotify.SetCoverArt(currentSong);
        }
        catch
        {
          // Exceptions will be handled (for telemetry etc.) within SetCoverArt, but they will be rethrown
          // so that we can set custom artwork here
          currentSong.CoverArtUrl = ALBUM_ACCESS_DENIED_ICON;
        }

        // Toastify-specific custom logic around album art (if it's missing, or an ad)
        UpdateSongForToastify(currentSong);

        toastIcon = currentSong.CoverArtUrl;

        this.Dispatcher.Invoke((Action)delegate { Title1.Text = currentSong.Track; Title2.Text = currentSong.Artist; }, System.Windows.Threading.DispatcherPriority.Normal);

        foreach (var p in this.Plugins)
        {
          try
          {
            p.TrackChanged(currentSong.Artist, currentSong.Track);
          }
          catch (Exception)
          {
            //For now we swallow any plugin errors.
          }
        }

        this.Dispatcher.Invoke((Action)delegate { FadeIn(); }, System.Windows.Threading.DispatcherPriority.Normal);

        if (SettingsXml.Current.SaveTrackToFile)
        {
          if (!string.IsNullOrEmpty(SettingsXml.Current.SaveTrackToFilePath))
          {
            try
            {
              string trackText = GetClipboardText(currentSong);

              File.WriteAllText(SettingsXml.Current.SaveTrackToFilePath, trackText);
            }
            catch { } // ignore errors writing out the album
          }
        }
      }
    }

    private void UpdateSongForToastify(Song currentSong)
    {
      if (string.IsNullOrWhiteSpace(currentSong.Track))
      {
        currentSong.CoverArtUrl = AD_PLAYING_ICON;

        currentSong.Track = "Spotify Ad";
      }
      else if (string.IsNullOrWhiteSpace(currentSong.CoverArtUrl))
      {
        currentSong.CoverArtUrl = DEFAULT_ICON;
      }
    }

    private void FadeIn(bool force = false, bool isUpdate = false)
    {
      if (minimizeTimer != null)
        minimizeTimer.Stop();

      if (dragging)
        return;

      SettingsXml settings = SettingsXml.Current;

      // this is a convenient place to reset the idle timer (if so asked)
      // as this will be triggered when a song is played. The primary problem is if there is a
      // particularly long song then this will not work. That said, this is the safest (in terms of 
      // not causing a user's computer from never sleeping).
      if (settings.PreventSleepWhilePlaying)
      {
#if DEBUG
        var rv =
#endif
                    WinHelper.SetThreadExecutionState(WinHelper.EXECUTION_STATE.ES_SYSTEM_REQUIRED);
#if DEBUG
        System.Diagnostics.Debug.WriteLine("** SetThreadExecutionState returned: " + rv);
#endif
      }

      if ((settings.DisableToast || settings.OnlyShowToastOnHotkey) && !force)
        return;

      isUpdateToast = isUpdate;

      if (!string.IsNullOrEmpty(toastIcon))
      {
        cover = new BitmapImage();
        cover.BeginInit();
        cover.UriSource = new Uri(toastIcon, UriKind.RelativeOrAbsolute);
        cover.EndInit();
        LogoToast.Source = cover;
      }

      this.WindowState = WindowState.Normal;

      this.Left = settings.PositionLeft;
      this.Top = settings.PositionTop;

      ResetPositionIfOffScreen();

      DoubleAnimation anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(250));
      anim.Completed += (s, e) => { FadeOut(); };
      this.BeginAnimation(Window.OpacityProperty, anim);

      this.Topmost = true;
    }

    private void ResetPositionIfOffScreen()
    {
      var rect = new System.Drawing.Rectangle((int)this.Left, (int)this.Top, (int)this.Width, (int)this.Height);

      if (!System.Windows.Forms.Screen.AllScreens.Any(s => s.WorkingArea.Contains(rect)))
      {
        // get the defaults, but don't save them (this allows the user to reconnect their screen and get their 
        // desired settings back)
        var position = ScreenHelper.GetDefaultToastPosition(this.Width, this.Height);

        this.Left = position.X;
        this.Top = position.Y;
      }
    }

    private void FadeOut(bool now = false)
    {
      // 16 == one frame (0 is not a valid interval)
      var interval = (now ? 16 : SettingsXml.Current.FadeOutTime);

      DoubleAnimation anim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(500))
      {
        BeginTime = TimeSpan.FromMilliseconds(interval)
      };
      this.BeginAnimation(Window.OpacityProperty, anim);

      if (minimizeTimer == null)
      {
        minimizeTimer = new Timer
        {
          AutoReset = false
        };

        minimizeTimer.Elapsed += (s, ev) =>
                {
                  Dispatcher.Invoke((Action)delegate
                  {
                    this.WindowState = WindowState.Minimized;

                    System.Diagnostics.Debug.WriteLine("Minimized");
                  });
                };
      }

      // extra buffer to avoid graphics corruption at the tail end of the fade
      minimizeTimer.Interval = interval * 2;

      minimizeTimer.Stop();
      minimizeTimer.Start();
    }

    void VersionChecker_CheckVersionComplete(object sender, CheckVersionCompleteEventArgs e)
    {
      if (!e.New)
        return;

      string title = "Update Toastify!";
      string caption = "Version " + e.Version + " available now.";

      // this is a background thread, so sleep it a bit so that it doesn't clash with the startup toast
      System.Threading.Thread.Sleep(20000);

      this.Dispatcher.Invoke((Action)delegate
      {
        Title1.Text = title;
        Title2.Text = caption;

        toastIcon = "SpotifyToastifyUpdateLogo.png";

        FadeIn(force: true, isUpdate: true);
      }, System.Windows.Threading.DispatcherPriority.Normal);
    }

    private void LoadPlugins()
    {
      //Load plugins
      this.Plugins = new List<Toastify.Plugin.IPluginBase>();
      string applicationPath = new System.IO.FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName;

      foreach (var p in SettingsXml.Current.Plugins)
      {
        try
        {
          var plugin = Activator.CreateInstanceFrom(System.IO.Path.Combine(applicationPath, p.FileName), p.TypeName).Unwrap() as Toastify.Plugin.IPluginBase;
          plugin.Init(p.Settings);
          this.Plugins.Add(plugin);
        }
        catch (Exception)
        {
          //For now we swallow any plugin errors.
        }
        Console.WriteLine("Loaded " + p.TypeName);
      }
    }

    private void AskUserToStartSpotify()
    {
      // TODO: Since WebDriver is no longer a thing, should this go back to being a user setting?
      //SettingsXml settings = SettingsXml.Current;

      // Thanks to recent changes in Spotify that removed the song Artist + Title from the titlebar
      // we are forced to launch Spotify ourselves (under WebDriver), so we no longer ask the user
      try
      {
        Spotify.StartSpotify();
      }
      catch (Exception e)
      {
        MessageBox.Show("An unknown error occurred when trying to start Spotify.\nPlease start Spotify manually.\n\nTechnical Details: " + e.Message, "Toastify", MessageBoxButton.OK, MessageBoxImage.Information);
      }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
      // close Spotify first
      if (SettingsXml.Current.CloseSpotifyWithToastify)
      {
        Spotify.KillSpotify();
      }

      // Ensure trayicon is removed on exit. (Thx Linus)
      trayIcon.Visible = false;
      trayIcon.Dispose();
      trayIcon = null;

      // Let the plugins now we're closing up.
      // we do this last since it's transparent to the user
      foreach (var p in this.Plugins)
      {
        try
        {
          p.Closing();
          p.Dispose();
        }
        catch (Exception)
        {
          //For now we swallow any plugin errors.
        }
      }

      this.Plugins.Clear();

      base.OnClosing(e);
    }

    #region ActionHookCallback

    private static Hotkey _lastHotkey = null;
    private static DateTime _lastHotkeyPressTime = DateTime.Now;

    /// <summary>
    /// If the same hotkey press happens within this buffer time, it will be ignored.
    /// 
    /// I came to 150 by pressing keys as quickly as possibly. The minimum time was less than 150
    /// but most values fell in the 150 to 200 range for quick presses, so 150 seemed the most reasonable
    /// </summary>
    private const int WAIT_BETWEEN_HOTKEY_PRESS = 150;

    internal static void ActionHookCallback(Hotkey hotkey)
    {
      // Bug 9421: ignore this keypress if it is the same as the previous one and it's been less than
      //           WAIT_BETWEEN_HOTKEY_PRESS since the last press. Note that we do not update 
      //           _lastHotkeyPressTime in this case to avoid being trapped in a never ending cycle of
      //           ignoring keypresses if the user (for some reason) decides to press really quickly, 
      //           really often on the hotkey
      if (hotkey == _lastHotkey && DateTime.Now.Subtract(_lastHotkeyPressTime).TotalMilliseconds < WAIT_BETWEEN_HOTKEY_PRESS)
        return;

      _lastHotkey = hotkey;
      _lastHotkeyPressTime = DateTime.Now;

      try
      {
        Song songBeforeAction = Toast.Current.currentSong;

        if (hotkey.Action == SpotifyAction.CopyTrackInfo && songBeforeAction != null)
        {
          Telemetry.TrackEvent(TelemetryCategory.Action, Telemetry.TelemetryEvent.Action.CopyTrackInfo);

          CopySongToClipboard(songBeforeAction);
        }
        else if (hotkey.Action == SpotifyAction.PasteTrackInfo && songBeforeAction != null)
        {
          Telemetry.TrackEvent(TelemetryCategory.Action, Telemetry.TelemetryEvent.Action.PasteTrackInfo);

          CopySongToClipboard(songBeforeAction);

          SendPasteKey();
        }
        else
        {
          Spotify.SendAction(hotkey.Action);
        }

        Toast.Current.DisplayAction(hotkey.Action, songBeforeAction);
      }
      catch (Exception ex)
      {
        if (System.Diagnostics.Debugger.IsAttached)
          System.Diagnostics.Debugger.Break();

        System.Diagnostics.Debug.WriteLine("Exception with hooked key! " + ex);
        Toast.Current.Title1.Text = "Unable to communicate with Spotify";
        Toast.Current.Title2.Text = "";
        Toast.Current.FadeIn();
      }
    }

    private static void SendPasteKey()
    {
      var shiftKey = new ManagedWinapi.KeyboardKey(System.Windows.Forms.Keys.ShiftKey);
      var altKey = new ManagedWinapi.KeyboardKey(System.Windows.Forms.Keys.Alt);
      var ctrlKey = new ManagedWinapi.KeyboardKey(System.Windows.Forms.Keys.ControlKey);
      var vKey = new ManagedWinapi.KeyboardKey(System.Windows.Forms.Keys.V);

      // Before injecting a paste command, first make sure that no modifiers are already
      // being pressed (which will throw off the Ctrl+v).
      // Since key state is notoriously unreliable, set a max sleep so that we don't get stuck
      var maxSleep = 250;

      // minimum sleep time
      System.Threading.Thread.Sleep(150);

      //System.Diagnostics.Debug.WriteLine("shift: " + shiftKey.State + " alt: " + altKey.State + " ctrl: " + ctrlKey.State);

      while (maxSleep > 0 && (shiftKey.State != 0 || altKey.State != 0 || ctrlKey.State != 0))
        System.Threading.Thread.Sleep(maxSleep -= 50);

      //System.Diagnostics.Debug.WriteLine("maxSleep: " + maxSleep);

      // press keys in sequence. Don't use PressAndRelease since that seems to be too fast
      // for most applications and the sequence gets lost.
      ctrlKey.Press();
      vKey.Press();
      System.Threading.Thread.Sleep(25);
      vKey.Release();
      System.Threading.Thread.Sleep(25);
      ctrlKey.Release();
    }

    private static string GetClipboardText(Song currentSong)
    {
      string trackBeforeAction = currentSong.ToString();
      var template = SettingsXml.Current.ClipboardTemplate;

      // if the string is empty we set it to {0}
      if (string.IsNullOrWhiteSpace(template))
        template = "{0}";

      // add the song name to the end of the template if the user forgot to put in the
      // replacement marker
      if (!template.Contains("{0}"))
        template += " {0}";

      return string.Format(template, trackBeforeAction);
    }

    private static void CopySongToClipboard(Song trackBeforeAction)
    {
      Clipboard.SetText(GetClipboardText(trackBeforeAction));
    }

    #endregion

    public void DisplayAction(SpotifyAction action, Song trackBeforeAction)
    {
      //Anything that changes track doesn't need to be handled since
      //that will be handled in the timer event.

      const string VOLUME_UP_TEXT = "Volume ++";
      const string VOLUME_DOWN_TEXT = "Volume --";
      const string MUTE_ON_OFF_TEXT = "Mute On/Off";
      const string NOTHINGS_PLAYING = "Nothing's playing";
      const string PAUSED_TEXT = "Paused";
      const string STOPPED_TEXT = "Stopped";
      const string SETTINGS_TEXT = "Settings saved";

      if (!Spotify.IsRunning() && action != SpotifyAction.SettingsSaved)
      {
        toastIcon = DEFAULT_ICON;
        Title1.Text = "Spotify not available!";
        Title2.Text = string.Empty;
        FadeIn();
        return;
      }

      Song currentTrack = trackBeforeAction;

      switch (action)
      {
        case SpotifyAction.PlayPause:
          if (trackBeforeAction != null)
          {
            //We pressed pause
            Title1.Text = PAUSED_TEXT;
            Title2.Text = trackBeforeAction.ToString();
            FadeIn();
          }
          currentSong = null;  //If we presses play this will force a toast to display in next timer event.
          break;
        case SpotifyAction.Stop:
          currentSong = null;
          Title1.Text = STOPPED_TEXT;
          Title2.Text = trackBeforeAction.ToString();
          FadeIn();
          break;
        case SpotifyAction.SettingsSaved:
          Title1.Text = SETTINGS_TEXT;
          Title2.Text = "Here is a preview of your settings!";
          FadeIn();
          break;
        case SpotifyAction.NextTrack:      //No need to handle
          break;
        case SpotifyAction.PreviousTrack:  //No need to handle
          break;
        case SpotifyAction.VolumeUp:
          Title1.Text = VOLUME_UP_TEXT;
          Title2.Text = currentTrack.ToString();
          FadeIn();
          break;
        case SpotifyAction.VolumeDown:
          Title1.Text = VOLUME_DOWN_TEXT;
          Title2.Text = currentTrack.ToString();
          FadeIn();
          break;
        case SpotifyAction.Mute:
          Title1.Text = MUTE_ON_OFF_TEXT;
          Title2.Text = currentTrack.ToString();
          FadeIn();
          break;
        case SpotifyAction.ShowToast:
          if (currentTrack == null || !currentTrack.IsValid())
          {
            toastIcon = DEFAULT_ICON;

            Title1.Text = NOTHINGS_PLAYING;
            Title2.Text = string.Empty;
          }
          else
          {
            if (currentTrack != null && currentTrack.IsValid())
            {
              toastIcon = currentTrack.CoverArtUrl;

              Title1.Text = currentTrack.Artist;
              Title2.Text = currentTrack.Track;
            }
          }

          FadeIn(force: true);
          break;
        case SpotifyAction.ShowSpotify:  //No need to handle
          break;
        case SpotifyAction.ThumbsUp:
          toastIcon = "Resources/thumbs_up.png";

          Title1.Text = "Thumbs Up!";
          Title2.Text = currentTrack.ToString();
          FadeIn();
          break;
        case SpotifyAction.ThumbsDown:
          toastIcon = "Resources/thumbs_down.png";

          Title1.Text = "Thumbs Down :(";
          Title2.Text = currentTrack.ToString();
          FadeIn();
          break;
      }
    }

    /// <summary>
    /// Mouse is over the window, halt any fade out animations and keep
    /// the toast active.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
      this.BeginAnimation(Window.OpacityProperty, null);
      this.Opacity = 1.0;
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
      FadeOut();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
      if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
      {
        dragging = true;
        DragMove();
        return;
      }

      FadeOut(now: true);

      if (isUpdateToast)
      {
        Process.Start(new ProcessStartInfo(versionChecker.UpdateUrl));
      }
      else
      {
        Spotify.SendAction(SpotifyAction.ShowSpotify);
      }
    }

    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
      if (dragging)
      {
        dragging = false;

        // save the new window position
        SettingsXml settings = SettingsXml.Current;

        settings.PositionLeft = this.Left;
        settings.PositionTop = this.Top;

        settings.Save();
      }
    }


  }
}
