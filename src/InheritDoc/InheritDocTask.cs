using System;

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

	public override bool Execute()
	{
		try
		{
			var logger = new TaskLogger(Log) as ILogger;
			logger.Write(ILogger.Severity.Info, typeof(InheritDocTask).Assembly.GetName().FullName);
			logger.Write(ILogger.Severity.Info, nameof(AssemblyPath) + ": " + AssemblyPath);
			logger.Write(ILogger.Severity.Info, nameof(DocumentationPath) + ": " + DocumentationPath);
			logger.Write(ILogger.Severity.Info, nameof(RefAssemblyPaths) + ": " + RefAssemblyPaths);
			logger.Write(ILogger.Severity.Info, nameof(AdditionalDocPaths) + ": " + AdditionalDocPaths);

			InheritDocProcessor.InheritDocs(AssemblyPath, DocumentationPath, RefAssemblyPaths?.Split(';') ?? Array.Empty<string>(), AdditionalDocPaths?.Split(';') ?? Array.Empty<string>(), DocumentationPath, logger);

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

		public TaskLogger(TaskLoggingHelper helper) => logger = helper;

		void ILogger.Write(ILogger.Severity severity, string msg)
		{
			if (severity == ILogger.Severity.Error)
				logger.LogError(msg);
			else if (severity == ILogger.Severity.Warn)
				logger.LogWarning(msg);
			else
				logger.LogMessage(severity == ILogger.Severity.Info ? MessageImportance.Normal : MessageImportance.Low, msg);
		}
	}
}
