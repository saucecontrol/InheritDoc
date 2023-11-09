// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

public class InheritDocTask : Task
{
	[Required]
	public string AssemblyPath { get; set; } = string.Empty;

	[Required]
	public string InDocPath { get; set; } = string.Empty;

	[Required]
	public string OutDocPath { get; set; } = string.Empty;

	public string? RefAssemblyPaths { get; set; }
	public string? AdditionalDocPaths { get; set; }
	public string? NoWarn { get; set; }
	public string? TrimLevel { get; set; }

	internal ILogger? Logger { get; set; }

	public override bool Execute()
	{
		try
		{
			var refPaths = RefAssemblyPaths?.Split(';') ?? [ ];
			var addPaths = AdditionalDocPaths?.Split(';') ?? [ ];
			var trim = (ApiLevel)Math.Min((int)(Enum.TryParse<ApiLevel>(TrimLevel, true, out var t) ? t : ApiLevel.Internal), (int)ApiLevel.Internal);

			string cmdargs = typeof(InheritDocTask).Assembly.GetName().FullName +
				Environment.NewLine + nameof(AssemblyPath) + ": " + AssemblyPath +
				Environment.NewLine + nameof(InDocPath) + ": " + InDocPath +
				Environment.NewLine + nameof(OutDocPath) + ": " + OutDocPath +
				Environment.NewLine + nameof(RefAssemblyPaths) + ": " + RefAssemblyPaths +
				Environment.NewLine + nameof(AdditionalDocPaths) + ": " + AdditionalDocPaths +
				Environment.NewLine + nameof(TrimLevel) + ": " + trim;

			Logger ??= new TaskLogger(Log, NoWarn?.Split(';') ?? [ ]);
			if (BuildEngine is not null)
				Log.LogCommandLine(MessageImportance.Normal, cmdargs);

			var (replaced, total, trimmed) = InheritDocProcessor.InheritDocs(AssemblyPath, InDocPath, OutDocPath, refPaths, addPaths, trim, Logger);

			Logger.Write(ILogger.Severity.Message, $"{nameof(InheritDocTask)} replaced {replaced} of {total} inheritdoc tags {(trim > ApiLevel.None ? $"and removed {trimmed} {(trim == ApiLevel.Private ? "private" : "non-public")} member docs " : null)}in {Path.GetFullPath(OutDocPath)}");
			return true;
		}
		catch (Exception ex)
		{
			if (BuildEngine is not null)
				Log.LogErrorFromException(ex, true);

			return false;
		}
	}

	private class TaskLogger(TaskLoggingHelper helper, ICollection<string> noWarn) : ILogger
	{
		private readonly TaskLoggingHelper logger = helper;
		private readonly ICollection<string> noWarning = noWarn;

		void ILogger.Write(ILogger.Severity severity, string msg)
		{
			var msgImportance = severity switch {
				ILogger.Severity.Message => MessageImportance.High,
				ILogger.Severity.Diag => MessageImportance.Low,
				_ => MessageImportance.Normal
			};

			if (severity == ILogger.Severity.Error)
				logger.LogError(msg);
			else
				logger.LogMessage(msgImportance, msg);
		}

		void ILogger.Warn(string code, string file, int line, int column, string msg)
		{
			if (noWarning.Contains(code))
				logger.LogMessage(MessageImportance.Normal, "Suppressed warning " + code + ": " + msg);
			else
				logger.LogWarning(nameof(InheritDocTask), code, null, file, line, column, 0, 0, msg);
		}
	}
}
