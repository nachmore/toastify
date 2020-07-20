﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Toastify
{
  internal class Win32
  {
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumThreadWindows(int threadId, EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
    internal static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

    [Flags()]
    internal enum SetWindowPosFlags : uint
    {
      /// <summary>If the calling thread and the thread that owns the window are attached to different input queues, 
      /// the system posts the request to the thread that owns the window. This prevents the calling thread from 
      /// blocking its execution while other threads process the request.</summary>
      /// <remarks>SWP_ASYNCWINDOWPOS</remarks>
      AsynchronousWindowPosition = 0x4000,
      /// <summary>Prevents generation of the WM_SYNCPAINT message.</summary>
      /// <remarks>SWP_DEFERERASE</remarks>
      DeferErase = 0x2000,
      /// <summary>Draws a frame (defined in the window's class description) around the window.</summary>
      /// <remarks>SWP_DRAWFRAME</remarks>
      DrawFrame = 0x0020,
      /// <summary>Applies new frame styles set using the SetWindowLong function. Sends a WM_NCCALCSIZE message to 
      /// the window, even if the window's size is not being changed. If this flag is not specified, WM_NCCALCSIZE 
      /// is sent only when the window's size is being changed.</summary>
      /// <remarks>SWP_FRAMECHANGED</remarks>
      FrameChanged = 0x0020,
      /// <summary>Hides the window.</summary>
      /// <remarks>SWP_HIDEWINDOW</remarks>
      HideWindow = 0x0080,
      /// <summary>Does not activate the window. If this flag is not set, the window is activated and moved to the 
      /// top of either the topmost or non-topmost group (depending on the setting of the hWndInsertAfter 
      /// parameter).</summary>
      /// <remarks>SWP_NOACTIVATE</remarks>
      DoNotActivate = 0x0010,
      /// <summary>Discards the entire contents of the client area. If this flag is not specified, the valid 
      /// contents of the client area are saved and copied back into the client area after the window is sized or 
      /// repositioned.</summary>
      /// <remarks>SWP_NOCOPYBITS</remarks>
      DoNotCopyBits = 0x0100,
      /// <summary>Retains the current position (ignores X and Y parameters).</summary>
      /// <remarks>SWP_NOMOVE</remarks>
      IgnoreMove = 0x0002,
      /// <summary>Does not change the owner window's position in the Z order.</summary>
      /// <remarks>SWP_NOOWNERZORDER</remarks>
      DoNotChangeOwnerZOrder = 0x0200,
      /// <summary>Does not redraw changes. If this flag is set, no repainting of any kind occurs. This applies to 
      /// the client area, the nonclient area (including the title bar and scroll bars), and any part of the parent 
      /// window uncovered as a result of the window being moved. When this flag is set, the application must 
      /// explicitly invalidate or redraw any parts of the window and parent window that need redrawing.</summary>
      /// <remarks>SWP_NOREDRAW</remarks>
      DoNotRedraw = 0x0008,
      /// <summary>Same as the SWP_NOOWNERZORDER flag.</summary>
      /// <remarks>SWP_NOREPOSITION</remarks>
      DoNotReposition = 0x0200,
      /// <summary>Prevents the window from receiving the WM_WINDOWPOSCHANGING message.</summary>
      /// <remarks>SWP_NOSENDCHANGING</remarks>
      DoNotSendChangingEvent = 0x0400,
      /// <summary>Retains the current size (ignores the cx and cy parameters).</summary>
      /// <remarks>SWP_NOSIZE</remarks>
      IgnoreResize = 0x0001,
      /// <summary>Retains the current Z order (ignores the hWndInsertAfter parameter).</summary>
      /// <remarks>SWP_NOZORDER</remarks>
      IgnoreZOrder = 0x0004,
      /// <summary>Displays the window.</summary>
      /// <remarks>SWP_SHOWWINDOW</remarks>
      ShowWindow = 0x0040,
    }

    internal struct WINDOWPLACEMENT
    {
      public int length;
      public int flags;
      public int showCmd;
      public System.Drawing.Point ptMinPosition;
      public System.Drawing.Point ptMaxPosition;
      public System.Drawing.Rectangle rcNormalPosition;
    }

    internal class Constants
    {
      internal const uint WM_APPCOMMAND = 0x0319;

      internal const int SW_SHOWMINIMIZED = 2;
      internal const int SW_SHOWNOACTIVATE = 4;
      internal const int SW_SHOWMINNOACTIVE = 7;
      internal const int SW_SHOW = 5;
      internal const int SW_RESTORE = 9;

      internal const int WM_CLOSE = 0x10;
      internal const int WM_QUIT = 0x12;
    }
  }
}
