using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.IO.Project;

/// <summary>What the Project Manager shows about a project file, read without loading all its pictures.</summary>
/// <param name="Preview">A small picture of the project (null for files saved before previews existed).</param>
public sealed record ProjectInfo(int PageCount, int Width, int Height, IReadOnlyList<string> PageNames, PixelBuffer? Preview);
