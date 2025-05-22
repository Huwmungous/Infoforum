// Advanced clipboard manager
using ClipboardSyncService;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System;

public class AdvancedClipboardManager
{
    public static List<ClipboardFormat> GetAllClipboardFormats()
    {
        var formats = new List<ClipboardFormat>();

        if (!ClipboardFormats.OpenClipboard(IntPtr.Zero))
            return formats;

        try
        {
            uint format = 0;
            while ((format = ClipboardFormats.EnumClipboardFormats(format)) != 0)
            {
                var data = GetClipboardFormatData(format);
                if (data != null)
                {
                    formats.Add(data);
                }
            }
        }
        finally
        {
            ClipboardFormats.CloseClipboard();
        }
        return formats;
    }

    private static ClipboardFormat GetClipboardFormatData(uint format)
    {
        var handle = ClipboardFormats.GetClipboardData(format);
        if (handle == IntPtr.Zero)
            return null;

        var size = ClipboardFormats.GlobalSize(handle);
        if (size == UIntPtr.Zero)
            return null;

        var ptr = ClipboardFormats.GlobalLock(handle);
        if (ptr == IntPtr.Zero)
            return null;

        try
        {
            var data = new byte[(int)size];
            Marshal.Copy(ptr, data, 0, (int)size);

            var formatName = GetFormatName(format);
            var metadata = new Dictionary<string, string>
            {
                ["size"] = size.ToString(),
                ["format_id"] = format.ToString()
            };

            return new ClipboardFormat
            {
                FormatName = formatName,
                Data = Google.Protobuf.ByteString.CopyFrom(data),
                Metadata = { metadata }
            };
        }
        finally
        {
            ClipboardFormats.GlobalUnlock(handle);
        }
    }

    public static void SetAllClipboardFormats(List<ClipboardFormat> formats)
    {
        if (!ClipboardFormats.OpenClipboard(IntPtr.Zero))
            return;

        try
        {
            ClipboardFormats.EmptyClipboard();

            foreach (var format in formats)
            {
                SetClipboardFormatData(format);
            }
        }
        finally
        {
            ClipboardFormats.CloseClipboard();
        }
    }

    private static void SetClipboardFormatData(ClipboardFormat format)
    {
        var formatId = GetFormatId(format.FormatName);
        if (formatId == 0)
            return;

        var data = format.Data.ToByteArray();
        var handle = ClipboardFormats.GlobalAlloc(ClipboardFormats.GMEM_MOVEABLE, (UIntPtr)data.Length);
        if (handle == IntPtr.Zero)
            return;

        var ptr = ClipboardFormats.GlobalLock(handle);
        if (ptr == IntPtr.Zero)
        {
            return;
        }

        try
        {
            Marshal.Copy(data, 0, ptr, data.Length);
            ClipboardFormats.GlobalUnlock(handle);
            ClipboardFormats.SetClipboardData(formatId, handle);
        }
        catch
        {
            ClipboardFormats.GlobalUnlock(handle);
            throw;
        }
    }

    private static string GetFormatName(uint format)
    {
        switch (format)
        {
            case ClipboardFormats.CF_TEXT: return "CF_TEXT";
            case ClipboardFormats.CF_UNICODETEXT: return "CF_UNICODETEXT";
            case ClipboardFormats.CF_BITMAP: return "CF_BITMAP";
            case ClipboardFormats.CF_HDROP: return "CF_HDROP";
            case ClipboardFormats.CF_DIB: return "CF_DIB";
            case ClipboardFormats.CF_ENHMETAFILE: return "CF_ENHMETAFILE";
            default:
                var sb = new StringBuilder(256);
                ClipboardFormats.GetClipboardFormatName(format, sb, sb.Capacity);
                return sb.ToString();
        }
    }

    private static uint GetFormatId(string formatName)
    {
        switch (formatName)
        {
            case "CF_TEXT": return ClipboardFormats.CF_TEXT;
            case "CF_UNICODETEXT": return ClipboardFormats.CF_UNICODETEXT;
            case "CF_BITMAP": return ClipboardFormats.CF_BITMAP;
            case "CF_HDROP": return ClipboardFormats.CF_HDROP;
            case "CF_DIB": return ClipboardFormats.CF_DIB;
            case "CF_ENHMETAFILE": return ClipboardFormats.CF_ENHMETAFILE;
            default:
                return ClipboardFormats.RegisterClipboardFormat(formatName);
        }
    }
}