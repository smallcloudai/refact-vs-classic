using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace RefactAI
{

    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(TestTag))]
    [ContentType("text")]
    internal sealed class MultilineGreyTextProvider : IViewTaggerProvider
    {

        // Disable "Field is never assigned to..." and "Field is never used" compiler's warnings. Justification: the field is used by MEF.
#pragma warning disable 649, 169

        /// <summary>
        /// Defines the adornment layer for the scarlet adornment. This layer is ordered
        /// after the selection layer in the Z-order
        /// </summary>
        [Export(typeof(AdornmentLayerDefinition))]
        [Name("RefactAI")]
        [Order(After = PredefinedAdornmentLayers.Caret)]
        private AdornmentLayerDefinition editorAdornmentLayer;

        [Import]
        internal SVsServiceProvider serviceProvider = null;

#pragma warning restore 649, 169

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            //create a single tagger for each buffer.
            Func<ITagger<T>> sc = delegate () { return new MultilineGreyTextTagger((IWpfTextView)textView, buffer) as ITagger<T>; };
            return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(typeof(MultilineGreyTextTagger), sc);
        }
    }
}
