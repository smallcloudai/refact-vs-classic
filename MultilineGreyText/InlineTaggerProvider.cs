using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using RefactAI;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactAI
{
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(IntraTextAdornmentTag))]
    [ContentType("text")]
    internal class InlineTaggerProvider : IViewTaggerProvider
    {
        //create a single tagger for each buffer.
        //the MultilineGreyTextTagger displays the grey text in the editor.
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            Func<ITagger<T>> sc = delegate () { return new InlineGreyTextTagger((IWpfTextView)textView) as ITagger<T>; };
            return buffer.Properties.GetOrCreateSingletonProperty(typeof(InlineGreyTextTagger), sc);
        }
    }
}
