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

	public override bool Execute()
	{
		try
		{
			var refPaths = RefAssemblyPaths?.Split(';') ?? Array.Empty<string>();
			var addPaths = AdditionalDocPaths?.Split(';') ?? Array.Empty<string>();
			var logger = new TaskLogger(Log, NoWarn?.Split(';') ?? Array.Empty<string>()) as ILogger;
			var trim = (ApiLevel)Math.Min((int)(Enum.TryParse<ApiLevel>(TrimLevel, true, out var t) ? t : ApiLevel.Internal), (int)ApiLevel.Internal);

			Log.LogCommandLine(MessageImportance.Normal,
				typeof(InheritDocTask).Assembly.GetName().FullName +
				Environment.NewLine + nameof(AssemblyPath) + ": " + AssemblyPath +
				Environment.NewLine + nameof(InDocPath) + ": " + InDocPath +
				Environment.NewLine + nameof(OutDocPath) + ": " + OutDocPath +
				Environment.NewLine + nameof(RefAssemblyPaths) + ": " + RefAssemblyPaths +
				Environment.NewLine + nameof(AdditionalDocPaths) + ": " + AdditionalDocPaths +
				Environment.NewLine + nameof(TrimLevel) + ": " + trim
			);

			var result = InheritDocProcessor.InheritDocs(AssemblyPath, InDocPath, OutDocPath, refPaths, addPaths, trim, logger);

			logger.Write(ILogger.Severity.Message, $"{nameof(InheritDocTask)} replaced {result.Item1} of {result.Item2} inheritdoc tags {(trim > ApiLevel.None ? $"and removed {result.Item3} {(trim == ApiLevel.Private ? "private" : "non-public")} member docs " : null)}in {Path.GetFullPath(OutDocPath)}");
			return true;
		}
		catch (Exception ex)
		{
			Log.LogErrorFromException(ex, true);
			return false;
		}
	}

	private class TaskLogger : ILogger
	{
		private readonly TaskLoggingHelper logger;
		private readonly ICollection<string> noWarning;

		public TaskLogger(TaskLoggingHelper helper, ICollection<string> noWarn)
		{
			logger = helper;
			noWarning = noWarn;
		}

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
