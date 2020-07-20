using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Diagnostics;
using SpotifyAPI.Web;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Toastify
{

  public enum SpotifyAction : long
  {
    None = 0,
    ShowToast = 1,
    ShowSpotify = 2,
    CopyTrackInfo = 3,
    SettingsSaved = 4,
    PasteTrackInfo = 5,
    ThumbsUp = 6,   // not usable, left in for future (hopefully!)
    ThumbsDown = 7, // not usable, left in for future (hopefully!)
    PlayPause = 917504,
    Mute = 524288,
    VolumeDown = 589824,
    VolumeUp = 655360,
    Stop = 851968,
    PreviousTrack = 786432,
    NextTrack = 720896,
    FastForward = 49 << 16,
    Rewind = 50 << 16,
  }

  public class Song
  {
    public string Artist { get; set; }
    public string Track { get; set; }
    public string Album { get; set; }

    public string CoverArtUrl { get; set; }

    public Song(string artist, string title, string album = null)
    {
      Artist = artist;
      Track = title;
      Album = album;
    }

    public override string ToString()
    {
      if (Artist == null)
        return Track;

      return string.Format("{0} - {1}", Artist, Track);
    }

    internal bool IsValid()
    {
      return (!string.IsNullOrEmpty(Artist) || !string.IsNullOrEmpty(Track));
    }

    public override bool Equals(object obj)
    {
      if (!(obj is Song target))
        return false;

      return (target.Artist == this.Artist && target.Track == this.Track);
    }

    // overriding GetHashCode is "required" when overriding Equals
    public override int GetHashCode()
    {
      return base.GetHashCode();
    }
  }

  static class Spotify
  {
    private static AutoHotkey.Interop.AutoHotkeyEngine _ahk;

    public static void StartSpotify()
    {
      if (IsRunning())
        return;

      string spotifyPath = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Spotify", string.Empty, string.Empty) as string;  //string.Empty = (Default) value

      // try in the secondary location
      if (string.IsNullOrEmpty(spotifyPath))
      {
        spotifyPath = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\Spotify", "InstallLocation", string.Empty) as string;  //string.Empty = (Default) value
      }

      if (string.IsNullOrEmpty(spotifyPath))
      {
        throw new ArgumentException("Could not find spotify path in registry");
      }

      // launch Spotify
      var spotifyExe = Path.Combine(spotifyPath, "spotify.exe");

      System.Diagnostics.Process.Start(spotifyExe);

      if (SettingsXml.Current.MinimizeSpotifyOnStartup)
      {
        Minimize();
      }
      else
      {

        // we need to let Spotify start up before interacting with it fully. 2 seconds is a relatively 
        // safe amount of time to wait, even if the pattern is gross. (Minimize() doesn't need it since
        // it waits for the Window to appear before minimizing)
        Thread.Sleep(2000);
      }
    }

    private static void Minimize()
    {
      var remainingSleep = 2000;

      IntPtr hWnd;

      // Since Minimize is often called during startup, the hWnd is often not created yet
      // wait a maximum of remainingSleep for it to appear and then minimize it if it did.
      while ((hWnd = Spotify.GetSpotify()) == IntPtr.Zero && remainingSleep > 0)
      {
        Thread.Sleep(100);
        remainingSleep -= 100;
      }

      if (hWnd != IntPtr.Zero)
      {
        // disgusting but sadly neccessary. Let Spotify initialize a bit before minimizing it
        // otherwise the window hides itself and doesn't respond to taskbar clicks.
        // I tried to work around this by waiting for the window size to initialize (via GetWindowRect)
        // but that didn't work, there is some internal initialization that needs to occur.
        Thread.Sleep(500);
        Win32.ShowWindow(hWnd, Win32.Constants.SW_SHOWMINIMIZED);
      }
    }

    private static void KillProc(string name)
    {
      // let's play nice and try to gracefully clear out all Sync processes
      var procs = System.Diagnostics.Process.GetProcessesByName(name);

      foreach (var proc in procs)
      {
        // lParam == Band Process Id, passed in below
        Win32.EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
        {
          Win32.GetWindowThreadProcessId(hWnd, out uint processId);

          // Essentially: Find every hWnd associated with this process and ask it to go away
          if (processId == (uint)lParam)
          {
            Win32.SendMessage(hWnd, Win32.Constants.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            Win32.SendMessage(hWnd, Win32.Constants.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
          }

          return true;
        },
        (IntPtr)proc.Id);
      }

      // let everything calm down
      Thread.Sleep(1000);

      procs = System.Diagnostics.Process.GetProcessesByName(name);

      // ok, no more mister nice guy. Sadly.
      foreach (var proc in procs)
      {
        try
        {
          proc.Kill();
        }
        catch { } // ignore exceptions (usually due to trying to kill non-existant child processes
      }
    }

    public static void KillSpotify()
    {
      KillProc("spotify");
    }

    /// <summary>
    /// Find a running instance of Spotify
    /// </summary>
    /// <returns>HWND of the Spotify window</returns>
    private static IntPtr GetSpotify()
    {
      var rv = IntPtr.Zero;

      // Spotify have made things a little harder with their use of electron
      // In order to not pick up on other Electron windows, first find the process and then the window

      // let's play nice and try to gracefully clear out all Sync processes
      var procs = System.Diagnostics.Process.GetProcessesByName("spotify");

      foreach (var proc in procs)
      {

        foreach (ProcessThread thread in proc.Threads)
        {
          Win32.EnumThreadWindows(thread.Id, (hWnd, lParam) =>
          {
            var sb = new StringBuilder(256);

            // get the class name to check if it's of type Chome_WidgetWin_0
            var ret = Win32.GetClassName(hWnd, sb, sb.Capacity);

            if (ret != 0)
            {
              if (sb.ToString() == "Chrome_WidgetWin_0")
              {

                // now check to make sure that it has a title (Spotify has a couple of windows
                // that it uses for specific controls, we don't want those
                ret = Win32.GetWindowText(hWnd, sb, sb.Capacity);
                if (ret != 0)
                {
                  if (!string.IsNullOrWhiteSpace(sb.ToString()))
                  {
                    rv = hWnd;
                  }
                }
              }
            }

            return true;
          }, IntPtr.Zero);

          if (rv != IntPtr.Zero)
          {
            return rv;
          }
        }
      }

      // couldn't find the window
      return rv;
    }

    public static bool IsRunning()
    {
      return (GetSpotify() != IntPtr.Zero);
    }

    public static Song GetCurrentSong()
    {
      if (!Spotify.IsRunning())
        return null;

      IntPtr hWnd = GetSpotify();
      int length = Win32.GetWindowTextLength(hWnd);
      StringBuilder sb = new StringBuilder(length + 1);
      Win32.GetWindowText(hWnd, sb, sb.Capacity);

      string title = sb.ToString();

      if (!string.IsNullOrWhiteSpace(title) && title != "Spotify")
      {
        // Unfortunately we don't have a great way to get the title from Spotify
        // so we need to do some gymnastics. 
        // Music played from an artist's page is usually in the format "artist - song" 
        // while music played from a playlist is often in the format "artist - song - album"
        // unfortunately this means that some songs that actually have a " - " in either the artist name
        // or in the song name will potentially display incorrectly
        var portions = title.Split(new string[] { " - " }, StringSplitOptions.None);

        var song = (portions.Length > 1 ? portions[1] : null);
        var artist = portions[0];
        var album = (portions.Length > 2 ? string.Join(" ", portions.Skip(2).ToArray()) : null); // take everything else that's left

        return new Song(artist, song, album);
      }

      return null;
    }

    public static async Task SetCoverArt(Song song)
    {
      // probably an ad, don't bother looking for an image
      if (string.IsNullOrWhiteSpace(song.Track) || string.IsNullOrWhiteSpace(song.Artist))
        return;

      // remove characters known to intefere with searching
      var reRemoveChars = new Regex("[/()\"]");

      var artist = reRemoveChars.Replace(song.Artist, "");
      var track = reRemoveChars.Replace(song.Track, "");

      var spotifyWeb = await SpotifyApiClient.GetAsync();

      var searchRequest = new SearchRequest(SearchRequest.Types.Track, $"{track} artist:{artist}");
      var searchResponse = await spotifyWeb.Search.Item(searchRequest);

      if (searchResponse.Tracks.Items.Count > 0)
      {
        var images = searchResponse.Tracks.Items[0].Album.Images;

        // iterate through all of the images, finding the smallest ones. This is usually the last
        // one, but there is no guarantee in the docs.
        var smallestWidth = int.MaxValue;
        string imageUrl = null;

        foreach (var image in images)
        {
          if (image.Width < smallestWidth)
          {
            imageUrl = image.Url;
          }
        }

        if (imageUrl != null)
        {
          song.CoverArtUrl = imageUrl;
        }
      }
    }

    private static bool IsMinimized()
    {
      if (!Spotify.IsRunning())
        return false;

      var hWnd = Spotify.GetSpotify();

      // check Spotify's current window state
      var placement = new Win32.WINDOWPLACEMENT();
      Win32.GetWindowPlacement(hWnd, ref placement);

      return (placement.showCmd == Win32.Constants.SW_SHOWMINIMIZED);
    }

    /*
     *TODO: Establish is this is needed. Could be useful to bring Spotify to the front without changing focus?
    private static void ShowSpotifyWithNoActivate()
    {
      var hWnd = Spotify.GetSpotify();

      // check Spotify's current window state
      var placement = new Win32.WINDOWPLACEMENT();
      Win32.GetWindowPlacement(hWnd, ref placement);

      var flags = Win32.SetWindowPosFlags.DoNotActivate | Win32.SetWindowPosFlags.DoNotChangeOwnerZOrder | Win32.SetWindowPosFlags.ShowWindow;

      Win32.SetWindowPos(hWnd, (IntPtr)0, placement.rcNormalPosition.Left, placement.rcNormalPosition.Top, 0, 0, flags);
    }
    */

    private static void ShowSpotify()
    {
      if (Spotify.IsRunning())
      {
        var hWnd = Spotify.GetSpotify();

        // check Spotify's current window state
        var placement = new Win32.WINDOWPLACEMENT();
        Win32.GetWindowPlacement(hWnd, ref placement);

        int showCommand = Win32.Constants.SW_SHOW;

        // if Spotify is minimzed we need to send a restore so that the window
        // will come back exactly like it was before being minimized (i.e. maximized
        // or otherwise) otherwise if we call SW_RESTORE on a currently maximized window
        // then instead of staying maximized it will return to normal size.
        if (placement.showCmd == Win32.Constants.SW_SHOWMINIMIZED)
        {
          showCommand = Win32.Constants.SW_RESTORE;
        }

        Win32.ShowWindow(hWnd, showCommand);

        Win32.SetForegroundWindow(hWnd);
        Win32.SetFocus(hWnd);
      }
    }

    public static void SendAction(SpotifyAction a)
    {
      if (!Spotify.IsRunning())
        return;

      // bah. Because control cannot fall through cases we need to special case volume
      if (SettingsXml.Current.ChangeSpotifyVolumeOnly)
      {
        if (a == SpotifyAction.VolumeUp)
        {
          Telemetry.TrackEvent(TelemetryCategory.Action, Telemetry.TelemetryEvent.Action.VolumeUp);

          VolumeHelper.IncrementVolume("Spotify");
          return;
        }
        else if (a == SpotifyAction.VolumeDown)
        {
          Telemetry.TrackEvent(TelemetryCategory.Action, Telemetry.TelemetryEvent.Action.VolumeDown);

          VolumeHelper.DecrementVolume("Spotify");
          return;
        }
        else if (a == SpotifyAction.Mute)
        {
          Telemetry.TrackEvent(TelemetryCategory.Action, Telemetry.TelemetryEvent.Action.Mute);

          VolumeHelper.ToggleApplicationMute("Spotify");
          return;
        }
      }

      switch (a)
      {
        case SpotifyAction.CopyTrackInfo:
        case SpotifyAction.ShowToast:
          //Nothing
          break;
        case SpotifyAction.ShowSpotify:
          Telemetry.TrackEvent(TelemetryCategory.Action, Telemetry.TelemetryEvent.Action.ShowSpotify);


          if (Spotify.IsMinimized())
          {
            ShowSpotify();
          }
          else
          {
            Minimize();
          }

          break;
        case SpotifyAction.FastForward:

          Telemetry.TrackEvent(TelemetryCategory.Action, Telemetry.TelemetryEvent.Action.FastForward);

          SendComplexKeys("+{Right}");
          break;

        case SpotifyAction.Rewind:

          Telemetry.TrackEvent(TelemetryCategory.Action, Telemetry.TelemetryEvent.Action.Rewind);

          SendComplexKeys("+{Left}");
          break;

        default:

          Telemetry.TrackEvent(TelemetryCategory.Action, Telemetry.TelemetryEvent.Action.Default + a.ToString());

          Win32.SendMessage(GetSpotify(), Win32.Constants.WM_APPCOMMAND, IntPtr.Zero, new IntPtr((long)a));
          break;
      }
    }

    /// <summary>
    /// Some commands require sending keys directly to Spotify (for example, Fast Forward and Rewind which
    /// are not handled by Spotify). We can't inject keys directly with WM_KEYDOWN/UP since we need a keyboard
    /// hook to actually change the state of various modifier keys (for example, Shift + Right for Fast Forward).
    /// 
    /// AutoHotKey has that hook and can modify the state for us, so let's take advantge of it.
    /// </summary>
    /// <param name="keys"></param>
    private static void SendComplexKeys(string keys)
    {
      // Is this nicer? 
      // _ahk = _ahk ?? new AutoHotkey.Interop.AutoHotkeyEngine();

      // only initialize AHK when needed as it can be expensive (dll copy etc) if not actually needed
      if (_ahk == null)
      {
        _ahk = new AutoHotkey.Interop.AutoHotkeyEngine();
      }

      _ahk.ExecRaw("SetTitleMatchMode 2");

      _ahk.ExecRaw("DetectHiddenWindows, On");
      _ahk.ExecRaw("ControlSend, ahk_parent, " + keys + ", ahk_class SpotifyMainWindow");

      _ahk.ExecRaw("DetectHiddenWindows, Off");
    }
  }
}
