using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PiusLauncher;

public static class FileIconService
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct SHFILEINFO
	{
		public nint hIcon;

		public int iIcon;

		public uint dwAttributes;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
		public string szDisplayName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
		public string szTypeName;
	}

	private const uint FileAttributeNormal = 128u;

	private const uint FileAttributeDirectory = 16u;

	private const uint ShgfiIcon = 256u;

	private const uint ShgfiLargeIcon = 0u;

	private const uint ShgfiUseFileAttributes = 16u;

	public static ImageSource? GetIcon(string? path)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		SHFILEINFO psfi = default(SHFILEINFO);
		bool num = Directory.Exists(path);
		bool flag = num || File.Exists(path);
		uint dwFileAttributes = (num ? 16u : 128u);
		uint num2 = 256u;
		if (!flag)
		{
			num2 |= 0x10;
		}
		if (SHGetFileInfo(path, dwFileAttributes, ref psfi, (uint)Marshal.SizeOf<SHFILEINFO>(), num2) == IntPtr.Zero || psfi.hIcon == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(psfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(48, 48));
			((Freezable)bitmapSource).Freeze();
			return bitmapSource;
		}
		finally
		{
			DestroyIcon(psfi.hIcon);
		}
	}

	[DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
	private static extern nint SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool DestroyIcon(nint hIcon);
}
