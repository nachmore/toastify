﻿using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Toastify
{
  class WinHelper
  {
    [Flags]
    public enum ExtendedWindowStyles
    {
      // ...
      WS_EX_TOOLWINDOW = 0x00000080,
      // ...
    }

    public enum GetWindowLongFields
    {
      // ...
      GWL_EXSTYLE = (-20),
      // ...
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

    private static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
      int error = 0;
      IntPtr result = IntPtr.Zero;
      // Win32 SetWindowLong doesn't clear error on success
      SetLastError(0);

      if (IntPtr.Size == 4)
      {
        // use SetWindowLong
        Int32 tempResult = IntSetWindowLong(hWnd, nIndex, IntPtrToInt32(dwNewLong));
        error = Marshal.GetLastWin32Error();
        result = new IntPtr(tempResult);
      }
      else
      {
        // use SetWindowLongPtr
        result = IntSetWindowLongPtr(hWnd, nIndex, dwNewLong);
        error = Marshal.GetLastWin32Error();
      }

      if ((result == IntPtr.Zero) && (error != 0))
      {
        throw new System.ComponentModel.Win32Exception(error);
      }

      return result;
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr IntSetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern Int32 IntSetWindowLong(IntPtr hWnd, int nIndex, Int32 dwNewLong);

    private static int IntPtrToInt32(IntPtr intPtr)
    {
      return unchecked((int)intPtr.ToInt64());
    }

    [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
    public static extern void SetLastError(int dwErrorCode);

    public static void AddToolWindowStyle(System.Windows.Window window)
    {
      WindowInteropHelper wndHelper = new WindowInteropHelper(window);
      int exStyle = (int)GetWindowLong(wndHelper.Handle, (int)WinHelper.GetWindowLongFields.GWL_EXSTYLE);
      exStyle |= (int)ExtendedWindowStyles.WS_EX_TOOLWINDOW;
      SetWindowLong(wndHelper.Handle, (int)GetWindowLongFields.GWL_EXSTYLE, (IntPtr)exStyle);
    }

    [FlagsAttribute]
    public enum EXECUTION_STATE : uint
    {
      ES_AWAYMODE_REQUIRED = 0x00000040,
      ES_CONTINUOUS = 0x80000000,
      ES_DISPLAY_REQUIRED = 0x00000002,
      ES_SYSTEM_REQUIRED = 0x00000001
      // Legacy flag, should not be used.
      // ES_USER_PRESENT = 0x00000004
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);
  }
}
