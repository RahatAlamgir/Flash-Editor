using FlashEditor.Core.Documents;

namespace FlashEditor.Core.IO.Project;

/// <summary>One page of a project: a picture with its own layers, plus the guide lines that belong to it.</summary>
public sealed record ProjectPage(string Name, Document Document, IReadOnlyList<double> VerticalGuides, IReadOnlyList<double> HorizontalGuides)
{
    public ProjectPage(string name, Document document) : this(name, document, Array.Empty<double>(), Array.Empty<double>()) { }
}

/// <summary>Everything in a project file.</summary>
public sealed record ProjectContent(IReadOnlyList<ProjectPage> Pages, int ActivePage);
