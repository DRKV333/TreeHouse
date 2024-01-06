using RazorBlade;
using RazorBlade.Support;

namespace PacketDocs.Templates;

internal abstract class InitHtmlTemplate<T> : HtmlTemplate<T>
{
    protected abstract void Init();

    [TemplateConstructor]
    protected InitHtmlTemplate(T model) : base(model)
    {
        Init();
    }
}