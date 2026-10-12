namespace Kamada.Web.Shared.Assistant;

/// <param name="Fraction">0 … 1.</param>
public readonly record struct DownloadProgress(double Fraction, string Text);
