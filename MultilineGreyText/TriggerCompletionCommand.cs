using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace RefactAI
{
    internal sealed class TriggerCompletionCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("528a0c75-4c23-4946-8b7f-b28afb34defc");

        private readonly AsyncPackage package;

        private TriggerCompletionCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static TriggerCompletionCommand Instance { get; private set; }

        private IAsyncServiceProvider ServiceProvider => this.package;

        public static async Task InitializeAsync(AsyncPackage package)
        {
            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new TriggerCompletionCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var completionBroker = componentModel.GetService<ICompletionBroker>();
            var textView = componentModel.GetService<IVsEditorAdaptersFactoryService>().GetWpfTextView(Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextView);

            if (textView != null)
            {
                var caretPosition = textView.Caret.Position.BufferPosition;
                completionBroker.TriggerCompletion(textView);
            }
        }
    }
}
