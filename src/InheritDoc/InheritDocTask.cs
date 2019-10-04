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
	public string DocumentationPath { get; set; } = string.Empty;

	public string? RefAssemblyPaths { get; set; }
	public string? AdditionalDocPaths { get; set; }
	public string? NoWarn { get; set; }

	public override bool Execute()
	{
		try
		{
			var logger = new TaskLogger(Log, NoWarn?.Split(';') ?? Array.Empty<string>()) as ILogger;
			logger.Write(ILogger.Severity.Info, null, typeof(InheritDocTask).Assembly.GetName().FullName);
			logger.Write(ILogger.Severity.Info, null, nameof(AssemblyPath) + ": " + AssemblyPath);
			logger.Write(ILogger.Severity.Info, null, nameof(DocumentationPath) + ": " + DocumentationPath);
			logger.Write(ILogger.Severity.Info, null, nameof(RefAssemblyPaths) + ": " + RefAssemblyPaths);
			logger.Write(ILogger.Severity.Info, null, nameof(AdditionalDocPaths) + ": " + AdditionalDocPaths);

			var refPaths = RefAssemblyPaths?.Split(';') ?? Array.Empty<string>();
			var addPaths = AdditionalDocPaths?.Split(';') ?? Array.Empty<string>();
			var result = InheritDocProcessor.InheritDocs(AssemblyPath, DocumentationPath, DocumentationPath, refPaths, addPaths, logger);

			logger.Write(ILogger.Severity.Message, null, $"{nameof(InheritDocTask)} replaced {result.Item1} of {result.Item2} tags in {Path.GetFullPath(DocumentationPath)}");
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

		void ILogger.Write(ILogger.Severity severity, string? code, string msg)
		{
			var msgImportance = severity switch {
				ILogger.Severity.Message => MessageImportance.High,
				ILogger.Severity.Diag => MessageImportance.Low,
				_ => MessageImportance.Normal
			};

			if (!string.IsNullOrEmpty(code))
				msg = code + ": " + msg;

			if (severity == ILogger.Severity.Error)
				logger.LogError(msg);
			else if (severity == ILogger.Severity.Warn && (code is null || !noWarning.Contains(code)))
				logger.LogWarning(msg);
			else
				logger.LogMessage(msgImportance, msg);
		}
	}
}
