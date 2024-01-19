﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using System.Windows.Shapes;

namespace RefactAI
{
    internal class InlineGreyTextTagger : ITagger<IntraTextAdornmentTag>
    {
        protected readonly IWpfTextView view;
        protected SnapshotSpan currentSpan;
        private Brush greyBrush;
        private StackPanel stackPanel;
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public InlineGreyTextTagger(IWpfTextView view){
            this.view = view;
            this.greyBrush = new SolidColorBrush(Colors.Gray);
            this.stackPanel = new StackPanel();
        }

        /// <param name="span">The span of text that this adornment will elide.</param>
        /// <returns>Adornment corresponding to given data. May be null.</returns>
        public void UpdateAdornment(UIElement text){
            ClearAdornment();
            stackPanel.Children.Add(text);
            stackPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            stackPanel.UpdateLayout();
        }

        public void ClearAdornment(){
            stackPanel.Children.Clear();
            stackPanel = new StackPanel();
        }

        public void FormatText(TextRunProperties props){
            if(props == null){
                return;
            }
            foreach (TextBlock block in stackPanel.Children){
                block.FontFamily = props.Typeface.FontFamily;
                block.FontSize = props.FontRenderingEmSize;
            }
        }

        public void MarkDirty(){
            var changeStart = view.TextViewLines.FirstVisibleLine.Start;
            var changeEnd = view.TextViewLines.LastVisibleLine.Start;

            var startLine = view.TextSnapshot.GetLineFromPosition(changeStart);
            var endLine = view.TextSnapshot.GetLineFromPosition(changeEnd);

            var span = new SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak).
                TranslateTo(targetSnapshot: view.TextBuffer.CurrentSnapshot, SpanTrackingMode.EdgePositive);

            TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(span.Start, span.End)));
        }

        // Produces tags on the snapshot that the tag consumer asked for.
        public virtual IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans){
            if (stackPanel.Children.Count == 0) {
                yield break;
            }

            ITextSnapshot requestedSnapshot = spans[0].Snapshot;
            double width = view.FormattedLineSource.ColumnWidth * ((stackPanel.Children[0] as TextBlock).Inlines.First() as Run).Text.Length;
            stackPanel.Measure(new Size(width, double.PositiveInfinity));
            stackPanel.MinWidth = width;
            stackPanel.MaxWidth = width;
            var caretLine = view.Caret.ContainingTextViewLine;
            SnapshotPoint point = view.Caret.Position.BufferPosition.TranslateTo(requestedSnapshot, PointTrackingMode.Positive);
            var line = requestedSnapshot.GetLineFromPosition(point);
            var span = new SnapshotSpan(point, point);

            IntraTextAdornmentTag tag = new IntraTextAdornmentTag(stackPanel, null, PositionAffinity.Successor);
            yield return new TagSpan<IntraTextAdornmentTag>(span, tag);
        }

    }
}
