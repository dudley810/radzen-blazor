using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Radzen.Blazor
{
    public partial class RadzenHtmlEditor : FormComponent<string>
    {
        [Parameter]
        public RenderFragment ChildContent { get; set; }

        [Parameter]
        public IDictionary<string, string> UploadHeaders { get; set; }

        [Parameter]
        public EventCallback<HtmlEditorPasteEventArgs> Paste { get; set; }

        [Parameter]
        public EventCallback<HtmlEditorExecuteEventArgs> Execute { get; set; }

        [Parameter]
        public string UploadUrl { get; set; }

        ElementReference ContentEditable { get; set; }

        internal RadzenHtmlEditorCommandState State { get; set; } = new RadzenHtmlEditorCommandState();

        async Task OnFocus()
        {
            await UpdateCommandState();
        }

        [JSInvokable]
        public async Task OnSelectionChange()
        {
            await UpdateCommandState();
        }

        [JSInvokable("GetHeaders")]
        public IDictionary<string, string> GetHeaders()
        {
            return UploadHeaders ?? new Dictionary<string, string>();
        }

        public async Task ExecuteCommandAsync(string name, string value = null)
        {
            State = await JSRuntime.InvokeAsync<RadzenHtmlEditorCommandState>("Radzen.execCommand", ContentEditable, name, value);
            await OnExecuteAsync(name);
            Html = State.Html;
            await OnChange();
        }

        async Task OnChange()
        {
            await Change.InvokeAsync(Html);
            await ValueChanged.InvokeAsync(Html);
        }

        internal async Task OnExecuteAsync(string name)
        {
            await Execute.InvokeAsync(new HtmlEditorExecuteEventArgs(this) { CommandName = name });

            StateHasChanged();
        }

        public async Task SaveSelectionAsync()
        {
            await JSRuntime.InvokeVoidAsync("Radzen.saveSelection", ContentEditable);
        }

        public async Task RestoreSelectionAsync()
        {
            await JSRuntime.InvokeVoidAsync("Radzen.restoreSelection", ContentEditable);
        }

        async Task UpdateCommandState()
        {
            State = await JSRuntime.InvokeAsync<RadzenHtmlEditorCommandState>("Radzen.queryCommands", ContentEditable);

            StateHasChanged();
        }

        async Task OnBlur()
        {
            await OnChange();
        }

        bool visibleChanged = false;
        bool firstRender = true;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            this.firstRender = firstRender;

            if (firstRender || visibleChanged)
            {
                visibleChanged = false;

                if (Visible)
                {
                    await JSRuntime.InvokeVoidAsync("Radzen.createEditor", ContentEditable, UploadUrl, Paste.HasDelegate, Reference);
                }
            }

            if (valueChanged)
            {
                valueChanged = false;

                Html = Value;

                if (Visible)
                {
                    await JSRuntime.InvokeVoidAsync("Radzen.innerHTML", ContentEditable, Value);
                }
            }
        }

        string Html { get; set; }

        protected override void OnInitialized()
        {
            Html = Value;
        }

        [JSInvokable]
        public void OnChange(string html)
        {
            Html = html;
        }

        [JSInvokable]
        public async Task<string> OnPaste(string html)
        {
            var args = new HtmlEditorPasteEventArgs { Html = html };

            await Paste.InvokeAsync(args);

            return args.Html;
        }

        bool valueChanged = false;

        public override async Task SetParametersAsync(ParameterView parameters)
        {
            if (parameters.DidParameterChange(nameof(Value), Value))
            {
                valueChanged = Html != parameters.GetValueOrDefault<string>(nameof(Value));
            }

            visibleChanged = parameters.DidParameterChange(nameof(Visible), Visible);

            await base.SetParametersAsync(parameters);

            if (visibleChanged && !firstRender && !Visible)
            {
                await JSRuntime.InvokeVoidAsync("Radzen.destroyEditor", ContentEditable);
            }
        }

        protected override string GetComponentCssClass()
        {
            return "rz-html-editor";
        }

        public override void Dispose()
        {
            base.Dispose();

            if (Visible && IsJSRuntimeAvailable)
            {
                JSRuntime.InvokeVoidAsync("Radzen.destroyEditor", ContentEditable);
            }
        }
    }
}