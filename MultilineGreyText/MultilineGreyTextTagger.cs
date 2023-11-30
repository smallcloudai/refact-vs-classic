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
using Microsoft.VisualStudio.TextManager.Interop;
using System.Reflection;
using Microsoft.VisualStudio.VCProjectEngine;
using System.Windows.Media.TextFormatting;
using Microsoft.VisualStudio.Settings.Internal;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

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
        private bool inlineSuggestion = false;
        private bool isTextInsertion = false;

        ///  line number the suggestion should be displayed at
        private int currentTextLineN;
        private int currentVisualLineN;
        private int suggestionIndex;
        private int insertionPoint;
        private int userIndex;
        private String userEndingText;
        /// suggestion to display
        /// first string is to match against second item: array is for formatting
        private static Tuple<String, String[]> suggestion = null;
     
        private InlineGreyTextTagger GetTagger(){
            var key = typeof(InlineGreyTextTagger);
            var props = view.TextBuffer.Properties;
            if (props.ContainsProperty(key)){
                return props.GetProperty<InlineGreyTextTagger>(key);
            }else{
                return null;
            }
        }

        public void SetSuggestion(String newSuggestion, bool inline, int caretPoint){
            ClearSuggestion();
            inlineSuggestion = inline;

            int lineN = GetCurrentTextLine();

            if (lineN < 0) return;

            String untrim = buffer.CurrentSnapshot.GetLineFromLineNumber(lineN).GetText();
            String line = untrim.TrimStart();
            int offset = untrim.Length - line.Length;

   /*         if (caretPoint > untrim.Length){
                
                newSuggestion = (new string(' ', caretPoint - untrim.Length)) + newSuggestion;
            }*/
            caretPoint = Math.Max(0, caretPoint - offset);
            
            String combineSuggestion = line + newSuggestion;
            if (line.Length - caretPoint > 0){
                String currentText = line.Substring(0, caretPoint);
                combineSuggestion = currentText + newSuggestion;
                userEndingText = line.TrimEnd().Substring(caretPoint);                
                var userIndex = newSuggestion.IndexOf(userEndingText);
                if(userIndex < 0){
                    return;
                }
                userIndex += currentText.Length;

                this.userIndex = userIndex;
                isTextInsertion = true;
                insertionPoint = line.Length - caretPoint;
            }else{
                isTextInsertion = false;
            }

            suggestion = new Tuple<String, String[]>(combineSuggestion, combineSuggestion.Split('\n'));
            Update();
        }

        private void CaretUpdate(object sender, CaretPositionChangedEventArgs e){
            if(showSuggestion && GetCurrentTextLine() != currentTextLineN){
                ClearSuggestion();
            }
        }

        private void LostFocus(object sender, EventArgs e){
            ClearSuggestion();
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
            view.LostAggregateFocus += LostFocus;
            view.Caret.PositionChanged += CaretUpdate;
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
           if (!showSuggestion || currentSuggestion == null || currentSuggestion.Item2.Length <= 1){
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

        TextRunProperties GetTextFormat(){
            var line = view.TextViewLines.FirstVisibleLine;
            return line.GetCharacterFormatting(line.Start);
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
        void FormatTextBlock(TextBlock textBlock){
            textBlock.FontStyle = FontStyles.Normal;
            textBlock.FontWeight = FontWeights.Normal;
        }

        TextBlock CreateTextBox(string text, Brush textColour){
            TextBlock textBlock = new TextBlock();
            textBlock.Inlines.Add(item: new Run(text) { Foreground = textColour});
            FormatTextBlock(textBlock);
            return textBlock;
        }

        void AddSuffixTextBlocks (int start, string line, string userText){
            if (line.Length <= suggestionIndex)
                return;

            int emptySpaceLength = userText.Length - userText.TrimStart().Length;
            string emptySpace = ConvertTabsToSpaces(userText.Substring(0, emptySpaceLength));
            string editedUserText = emptySpace + userText.TrimStart();
            if (isTextInsertion){
                editedUserText = emptySpace + line.Substring(0, start);
            }
            string remainder = line.Substring(start);
            TextBlock textBlock = new TextBlock();
            textBlock.Inlines.Add(item: new Run(editedUserText) { Foreground = transparentBrush });
            textBlock.Inlines.Add(item: new Run(remainder) { Foreground = greyBrush });

            stackPanel.Children.Add(textBlock);
        }

        void AddInsertionTextBlock(int start, int end, string line){
            if (line.Length <= suggestionIndex)
                return;

            TextBlock textBlock = new TextBlock();
            string remainder = line.Substring(start, end - start);
            GetTagger().UpdateAdornment(CreateTextBox(remainder, greyBrush));
        }


        //Updates the grey text
        public void UpdateAdornment(IWpfTextView view, string userText, int suggestionStart){
            stackPanel.Children.Clear();
            GetTagger().ClearAdornment();
            for (int i = suggestionStart; i < suggestion.Item2.Length; i++){
                string line = suggestion.Item2[i];

                if (i == 0){
                    int offset = line.Length - line.TrimStart().Length;

                    if (isTextInsertion && suggestionIndex < userIndex){
                        if(suggestionIndex > 0 && char.IsWhiteSpace(line[suggestionIndex - 1]) && !char.IsWhiteSpace(userText[userText.Length - insertionPoint - 1])){
                            suggestionIndex--;
                        }
                        AddInsertionTextBlock(suggestionIndex + offset, userIndex, line);
                        if (line.Length > userIndex + 1) {
                            AddSuffixTextBlocks(userIndex + userEndingText.Trim().Length, line, userText);
                        }
                    }else{
                        AddSuffixTextBlocks(userText.Length > 0 ? suggestionIndex + offset : 0, line, userText);
                    }
                }else{
                    stackPanel.Children.Add(CreateTextBox(line, greyBrush));
                }
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
            
            GetTagger().FormatText(GetTextFormat());
            if (stackPanel.Children.Count > 0){
                // Clear the adornment layer of previous adornments
                this.adornmentLayer.RemoveAllAdornments();

                ITextSnapshotLine snapshotLine = view.TextSnapshot.GetLineFromLineNumber(currentTextLineN);
                var start = view.TextViewLines.GetCharacterBounds(snapshotLine.Start);

                var span = snapshotLine.Extent;

                // Place the image in the top left hand corner of the line
                Canvas.SetLeft(stackPanel, start.Left);
                Canvas.SetTop(element: stackPanel, start.Top);

                // Add the image to the adornment layer and make it relative to the viewport
                this.adornmentLayer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, stackPanel, null);
            }
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

        bool IsNameChar(char c){
            return Char.IsLetterOrDigit(c) || c == '_';
        }

        //Compares the two strings to see if a is a prefix of b ignoring whitespace
        Tuple<int,int> CompareStrings(String a, String b){
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

                    if (Char.IsWhiteSpace(aChar) && (b_index >= 1 && !IsNameChar(b[b_index - 1]))){
                        a_index = nextNonWhitespace(a, a_index);

                        continue;
                    }

                    break;
                }
            }

            return new Tuple<int, int>(a_index, b_index);
        }

        //Check if the text in the editor is a substring of the the suggestion text 
        //If it matches return the line number of the suggestion text that matches the current line in the editor 
        //else return -1
        int CheckSuggestion(String suggestion, String line){
            if (line.Length == 0){
                return 0;
            }

            int index = suggestion.IndexOf(line);
            int endPos = index + line.Length;
            int firstLineBreak = suggestion.IndexOf('\n');

            if (index > -1 && (firstLineBreak == -1 || endPos < firstLineBreak)){
                return index == 0 ? line.Length : -1;
            }else{
                Tuple<int,int> res = CompareStrings(line, suggestion);
                int endPoint = isTextInsertion ? line.Length - insertionPoint : line.Length;
                return res.Item1 >=  endPoint ? res.Item2 : -1;
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

            int suggestionIndex = CheckSuggestion( suggestion.Item1, line);
            if (suggestionIndex >= 0){
                this.currentTextLineN = textLineN;
                this.suggestionIndex = suggestionIndex;
                ShowSuggestion(untrimLine, 0);
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

            int suggestionLineN = CheckSuggestion( suggestion.Item1, line);
            if(suggestionLineN >= 0){
                int diff = untrimLine.Length - untrimLine.TrimStart().Length;
                string whitespace = String.IsNullOrWhiteSpace(untrimLine) ? "" : untrimLine.Substring(0, diff);
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
        void ShowSuggestion(String text,  int suggestionLineStart){
            UpdateAdornment(view, text, suggestionLineStart);

            showSuggestion = true;
            MarkDirty();
        }

        //removes the suggestion
        public void ClearSuggestion(){
            if (!showSuggestion) return;
            InlineGreyTextTagger inlineTagger = GetTagger();
            inlineTagger.ClearAdornment();
            inlineTagger.MarkDirty();
            suggestion = null;
            adornmentLayer.RemoveAllAdornments();
            showSuggestion = false;

            MarkDirty();
        }

        //triggers refresh of the screen 
        void MarkDirty(){
            GetTagger().MarkDirty();
            ITextSnapshot newSnapshot = buffer.CurrentSnapshot;
            this.snapshot = newSnapshot;
           
            if(view.TextViewLines == null) return;

            var changeStart = view.TextViewLines.FirstVisibleLine.Start;
            var changeEnd = view.TextViewLines.LastVisibleLine.Start;

            var startLine = view.TextSnapshot.GetLineFromPosition(changeStart);
            var endLine = view.TextSnapshot.GetLineFromPosition(changeEnd);

            var span = new SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak).
            TranslateTo(targetSnapshot: newSnapshot, SpanTrackingMode.EdgePositive);

            //lines we are marking dirty
            //currently all of them for simplicity 
            if (this.TagsChanged != null){
                this.TagsChanged(this, new SnapshotSpanEventArgs(span)); 
            }
        }
    }
}