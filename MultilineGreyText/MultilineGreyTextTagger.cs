using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using System.Diagnostics;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using System.Text.RegularExpressions;
using System.Linq;

namespace RefactAI{

    internal sealed class MultilineGreyTextTagger : ITagger<TestTag>{
        /// panel with multiline grey text
        private StackPanel stackPanel;

        /// used to set the colour of the grey text
        private Brush greyBrush;

        /// used to set the colour of text that overlaps with the users text
        private Brush transparentBrush;

        /// contains the editor text and OnChange triggers on any text changes
        ITextBuffer buffer;

        /// current editor display, immutable data
        ITextSnapshot snapshot;

        /// the editor display object
        IWpfTextView view;

        /// contains the grey text
        private IAdornmentLayer adornmentLayer;

        /// true if a suggestion should be shown
        private bool showSuggestion = false;

        ///  line number the suggestion should be displayed at
        private int currentTextLineN;
        private int currentVisualLineN;
        private int suggestionIndex;

        /// suggestion to display
        /// first string is to match against second item: array is for formatting
        private static Tuple<String, String[]> suggestion = null;

        public void SetSuggestion(String newSuggestion){
            ClearSuggestion();

            CaretPosition caretPosition = view.Caret.Position;
            var point = caretPosition.Point.GetPoint(buffer, caretPosition.Affinity);

            if (!point.HasValue) return;

            ITextSnapshot newSnapshot = buffer.CurrentSnapshot;
            int lineN = newSnapshot.GetLineNumberFromPosition(point.Value);

            String line = newSnapshot.GetLineFromLineNumber(lineN).GetText().Trim();

            String combineSuggestion = line + newSuggestion;
            suggestion = new Tuple<String, String[]>(combineSuggestion, combineSuggestion.Split('\n'));
            Update();
        }

        public MultilineGreyTextTagger(IWpfTextView view, ITextBuffer buffer){
            this.stackPanel = new StackPanel();

            this.buffer = buffer;
            this.snapshot = buffer.CurrentSnapshot;
            this.buffer.Changed += BufferChanged;
            this.view = view;
            this.adornmentLayer = view.GetAdornmentLayer("RefactAI");

            this.view.LayoutChanged += this.OnSizeChanged;

            this.transparentBrush = new SolidColorBrush();
            this.transparentBrush.Opacity = 0;
            this.greyBrush = new SolidColorBrush(Colors.Gray);
        }

        public bool IsSuggestionActive(){
            return showSuggestion;
        }

        public String GetSuggestion(){
            if(suggestion != null && showSuggestion){
                return suggestion.Item1;
            }else{
                return "";
            }
        }

        //This an iterator that is used to iterate through all of the test tags
        //tags are like html tags they mark places in the view to modify how those sections look
        //Testtag is a tag that tells the editor to add empty space
        public IEnumerable<ITagSpan<TestTag>> GetTags(NormalizedSnapshotSpanCollection spans){
           var currentSuggestion = suggestion;
           if (!showSuggestion || currentSuggestion.Item2.Length <= 1){
                yield break;
           }

            SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(snapshot, SpanTrackingMode.EdgeExclusive);
            ITextSnapshot currentSnapshot = spans[0].Snapshot;
            
            var line = currentSnapshot.GetLineFromLineNumber(currentTextLineN).Extent;
            var span = new SnapshotSpan(line.End, line.End);

            var snapshotLine = currentSnapshot.GetLineFromLineNumber(currentTextLineN);

            var height = view.LineHeight * (currentSuggestion.Item2.Length - 1);

            if(currentTextLineN == 0 && currentSnapshot.Lines.Count() == 1 && String.IsNullOrEmpty(currentSnapshot.GetText())){
                height += view.LineHeight;
            }

            yield return new TagSpan<TestTag>(span,new TestTag(0, 0, 0, 0, height, PositionAffinity.Predecessor, stackPanel, this));
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        //triggers when the editor text buffer changes
        void BufferChanged(object sender, TextContentChangedEventArgs e){
            // If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event).
            if (e.After != buffer.CurrentSnapshot)
                return;
            this.Update();
        }

        //used to set formatting of the displayed multi lines
        public void FormatText(TextBlock block){
            //var pos = snapshot.GetLineFromLineNumber(currentLineN).Start;

            var line = view.TextViewLines.FirstVisibleLine;
            var format = line.GetCharacterFormatting(line.Start);
            if(format != null){
                block.FontFamily = format.Typeface.FontFamily;
                block.FontSize = format.FontRenderingEmSize;
            }
        }

        String ConvertTabsToSpaces(string text){
            int tabSize = view.Options.GetTabSize();
            return Regex.Replace(text, "\t", new string(' ', tabSize));
        }

        //Updates the grey text
        public void UpdateAdornment(IWpfTextView view, string userText, int suggestionStart){
            stackPanel.Children.Clear();
            for (int i = suggestionStart; i < suggestion.Item2.Length; i++){
                string line = suggestion.Item2[i];

                TextBlock textBlock = new TextBlock();

                if (i == 0){
                    string emptySpace = ConvertTabsToSpaces(userText.Substring(0, userText.Length - userText.TrimStart().Length));
                    string editedUserText = emptySpace + userText.TrimStart();
                    textBlock.Inlines.Add(item: new Run(editedUserText) { Foreground = transparentBrush });

                    if(line.Length > suggestionIndex){
                        int offset = line.Length - line.TrimStart().Length;
                        string remainder = line.Substring(suggestionIndex + offset);
                        textBlock.Inlines.Add(item: new Run(remainder) { Foreground = greyBrush });
                    }
                }else{
                    textBlock.Inlines.Add(item: new Run(line));
                    textBlock.Foreground = new SolidColorBrush(Colors.Gray);
                }

                textBlock.FontStyle = FontStyles.Normal;
                textBlock.FontWeight = FontWeights.Normal;

                stackPanel.Children.Add(textBlock);
            }

            this.adornmentLayer.RemoveAllAdornments();

            //usually only happens the moment a bunch of text has rentered such as an undo operation
            try{
                ITextSnapshotLine snapshotLine = view.TextSnapshot.GetLineFromLineNumber(currentTextLineN);
                var start = view.TextViewLines.GetCharacterBounds(snapshotLine.Start);

                // Place the image in the top left hand corner of the line
                Canvas.SetLeft(stackPanel, start.Left);
                Canvas.SetTop(stackPanel, start.Top);
                var span = snapshotLine.Extent;
                // Add the image to the adornment layer and make it relative to the viewport
                this.adornmentLayer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, stackPanel, null);

            }catch(ArgumentOutOfRangeException e){
                Debug.Write(e);
            }
        }

        //Adds grey text to display
        private void OnSizeChanged(object sender, EventArgs e){
            if (!showSuggestion){
                return;
            }

            foreach (TextBlock block in stackPanel.Children){
                FormatText(block);
            }

            // Clear the adornment layer of previous adornments
            this.adornmentLayer.RemoveAllAdornments();

            ITextSnapshotLine snapshotLine = view.TextSnapshot.GetLineFromLineNumber(currentTextLineN);
            var start = view.TextViewLines.GetCharacterBounds(snapshotLine.Start);

            // Place the image in the top left hand corner of the line
            Canvas.SetLeft(stackPanel, start.Left);
            Canvas.SetTop(stackPanel, start.Top);

            var span = snapshotLine.Extent;

            // Add the image to the adornment layer and make it relative to the viewport
            this.adornmentLayer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, stackPanel, null);
        }

        //returns the number of times letter c appears in s
        int GetOccurenceOfLetter(String s, char c){ 
            int n = 0;
            for (int i = 0; (i = s.IndexOf(c, i)) >= 0; i++, n++){}
            return n;
        }

        //skips whitespace
        int nextNonWhitespace(String s, int index){
            for (; index < s.Length && Char.IsWhiteSpace(s[index]); index++) ; ;
            return index;
        }

        //Compares the two strings to see if a is a prefix of b ignoring whitespace
        int CompareStrings(String a, String b){
            int a_index = 0, b_index = 0;
            while(a_index < a.Length && b_index < b.Length){
                char aChar = a[a_index];
                char bChar = b[b_index];
                if (aChar == bChar){
                    a_index++;
                    b_index++;
                }else{
                    if (Char.IsWhiteSpace(bChar)){
                        b_index = nextNonWhitespace(b, b_index);

                        continue;
                    }

                    if (Char.IsWhiteSpace(aChar) && (b_index >= 1 && Char.IsWhiteSpace(b[b_index - 1]))){
                        a_index = nextNonWhitespace(a, a_index);

                        continue;
                    }

                    return -1;
                }
            }

            return a_index >= a.Length ? b_index : -1;
        }

        //Check if the text in the editor is a substring of the the suggestion text 
        //If it matches return the line number of the suggestion text that matches the current line in the editor 
        //else return -1
        int CheckSuggestion(ITextSnapshot newSnapshot, String suggestion, String line, int startLine){
            if (line.Length == 0){
                return 0;
            }

            int index = suggestion.IndexOf(line);
            int endPos = index + line.Length;
            int firstLineBreak = suggestion.IndexOf('\n');

            if (index > -1 && (firstLineBreak == -1 || endPos < firstLineBreak)){
                return index == 0 ? line.Length : -1;
            }else{
                int res = CompareStrings(line, suggestion);
                return res >= 0 ? res : -1;
            }
        }

        //Gets the line number of the caret
        int GetCurrentTextLine(){
            CaretPosition caretPosition = view.Caret.Position;

            var textPoint = caretPosition.Point.GetPoint(buffer, caretPosition.Affinity);

            if (!textPoint.HasValue){
                return -1;
            }

            return buffer.CurrentSnapshot.GetLineNumberFromPosition(textPoint.Value);
        }

        //update multiline data
        public void Update(){

            if(suggestion == null){
                return;
            }

            int textLineN = GetCurrentTextLine();

            if(textLineN < 0){
                return;
            }

            ITextSnapshot newSnapshot = buffer.CurrentSnapshot;
            this.snapshot = newSnapshot;

            String untrimLine = newSnapshot.GetLineFromLineNumber(textLineN).GetText();
            String line = untrimLine.TrimStart();

            //get line carat is on
            //if suggestion matches line (possibly including preceding lines)
            //  show suggestion
            //else
            //  clear suggestions

            int suggestionIndex = CheckSuggestion(newSnapshot, suggestion.Item1, line, textLineN);
            if (suggestionIndex >= 0){
                this.currentTextLineN = textLineN;
                this.suggestionIndex = suggestionIndex;
                ShowSuggestion(untrimLine, suggestion.Item2, 0);
            }else{
                ClearSuggestion();
            }
        }

        //Adds the grey text to the file replacing current line in the process
        public bool CompleteText(){
            int textLineN = GetCurrentTextLine();

            if (textLineN < 0 || textLineN != currentTextLineN){
                return false;
            }

            String untrimLine = this.snapshot.GetLineFromLineNumber(currentTextLineN).GetText();
            String line = untrimLine.Trim();

            int suggestionLineN = CheckSuggestion(this.snapshot, suggestion.Item1, line, currentTextLineN);
            if(suggestionLineN >= 0){
                int diff = untrimLine.Length - untrimLine.TrimStart().Length;
                string whitespace = untrimLine.Substring(0, diff);
                ReplaceText(whitespace + suggestion.Item1, currentTextLineN);
                return true;
            }

            return false;
        }

        //replaces text in the editor
        void ReplaceText(string text, int lineN){
            ClearSuggestion();

            SnapshotSpan span = this.snapshot.GetLineFromLineNumber(lineN).Extent;
            ITextEdit edit = view.TextBuffer.CreateEdit();

            edit.Replace(span, text);
            edit.Apply();
        }

        //sets up the suggestion for display
        void ShowSuggestion(String text, String[] suggestion, int suggestionLineStart){
            UpdateAdornment(view, text, suggestionLineStart);
            showSuggestion = true;
            MarkDirty();
        }

        //removes the suggestion
        public void ClearSuggestion(){
            if(!showSuggestion) return;
            suggestion = null;
            adornmentLayer.RemoveAllAdornments();
            showSuggestion = false;

            MarkDirty();
        }

        //triggers refresh of the screen 
        void MarkDirty(){
            ITextSnapshot newSnapshot = buffer.CurrentSnapshot;
            this.snapshot = newSnapshot;
           
            if(view.TextViewLines == null) return;

            var changeStart = view.TextViewLines.FirstVisibleLine.Start;
            var changeEnd = view.TextViewLines.LastVisibleLine.Start;

            var startLine = view.TextSnapshot.GetLineFromPosition(changeStart);
            var endLine = view.TextSnapshot.GetLineFromPosition(changeEnd);

            var span = new SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak).
            TranslateTo(newSnapshot, SpanTrackingMode.EdgePositive);

            //lines we are marking dirty
            //currently all of them for simplicity 
            if (this.TagsChanged != null){
                this.TagsChanged(this, new SnapshotSpanEventArgs(span)); 
            }
        }
    }
}