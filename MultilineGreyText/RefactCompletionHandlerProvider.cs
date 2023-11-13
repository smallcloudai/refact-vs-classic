using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace RefactAI{

    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal class RefactCompletionHandlerProvider : IVsTextViewCreationListener{ 

        //adapters are used to get the IVsTextViewAdapter from the IVsTextView
        [Import]
        internal IVsEditorAdaptersFactoryService AdapterService = null;

        //CompletionBroker is used by intellisense (popups) to provide completion items.
        [Import]
        internal ICompletionBroker CompletionBroker { get; set; }

        //service provider is used to get the IVsServiceProvider which is needed to access lsp 
        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }
        
        //document factory is used to get information about the current text document such as filepath, language, etc.
        [Import] 
        internal ITextDocumentFactoryService documentFactory = null;

        //Called when a text view is created used to set up the key handler for the new view.
        public void VsTextViewCreated(IVsTextView textViewAdapter){
            ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);
            if (textView == null)
                return;

            Func<RefactCompletionCommandHandler> createCommandHandler = delegate () { return new RefactCompletionCommandHandler(textViewAdapter, textView, this); };
            textView.Properties.GetOrCreateSingletonProperty( createCommandHandler);
        }
    }
}
