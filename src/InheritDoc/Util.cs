// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

internal static class Util
{
	public static HashSet<T> ToHashSet<T>(this IEnumerable<T> e) => new(e);

	public static IEnumerable<T> SelectManyRecursive<T>(this IEnumerable<T> e, Func<T, IEnumerable<T>> selector) =>
		e.Any() ? e.Concat(e.SelectMany(selector).SelectManyRecursive(selector)) : e;

	public static bool HasAttribute(this XElement e, XName name) => e.Attribute(name) is not null;

	public static int SourceLine(this XElement e) => e is IXmlLineInfo li && li.HasLineInfo() ? li.LineNumber : 0;

	public static int SourceColumn(this XElement e) => e is IXmlLineInfo li && li.HasLineInfo() ? li.LinePosition : 0;

	public static bool IsWhiteSpace(this XNode n) =>
		(n.NodeType is XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace) ||
		(n.NodeType is XmlNodeType.Text && string.IsNullOrWhiteSpace(((XText)n).Value));

	public static IEnumerable<XNode> XPathSelectNodes(this XElement e, string xpath) =>
		(e.XPathEvaluate(xpath) as IEnumerable)?.Cast<XNode>() ?? Enumerable.Empty<XNode>();

	// https://github.com/dotnet/roslyn/issues/13529#issuecomment-245097691
	public static string ReplacePathSpecialFolder(string path)
	{
		const string PROGRAMFILES_TOKEN = "%PROGRAMFILESDIR%";
		const string FOLDERID_ProgramFilesX86 = "7c5a40ef-a0fb-4bfc-874a-c0f2e0b9fa8e";
		const string GUIDFORMAT_DIGITS_WITH_HYPHEN = "D";
		const int S_OK = 0;

		if (
			RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			&& path.StartsWith(PROGRAMFILES_TOKEN)
			&& SHGetKnownFolderPath(Guid.ParseExact(FOLDERID_ProgramFilesX86, GUIDFORMAT_DIGITS_WITH_HYPHEN), 0, IntPtr.Zero, out IntPtr pPath) == S_OK
		)
		{
			path = Path.Combine(Marshal.PtrToStringUni(pPath)!, path.Replace(PROGRAMFILES_TOKEN, null));
			Marshal.FreeCoTaskMem(pPath);
		}

		return path;
	}

	[DllImport("shell32", CharSet = CharSet.Unicode)]
	public static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);
}
