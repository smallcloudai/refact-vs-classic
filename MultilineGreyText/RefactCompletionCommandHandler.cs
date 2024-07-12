using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace RefactAI{

    internal class RefactCompletionCommandHandler : IOleCommandTarget{
      
        //LanguageClientMetadata is needed to manually load LSP
        private class LanguageClientMetadata : ILanguageClientMetadata{
            public LanguageClientMetadata(string[] contentTypes, string clientName = null){
                this.ContentTypes = contentTypes;
                this.ClientName = clientName;
            }

            public string ClientName { get; }

            public IEnumerable<string> ContentTypes { get; }
        }

        private IOleCommandTarget m_nextCommandHandler;
        private ITextView m_textView;
        private ICompletionSession m_session;
        private IVsTextView textViewAdapter;
        private ITextDocument doc;

        private RefactCompletionHandlerProvider m_provider;
        private RefactLanguageClient client = null;
        private String filePath;
        private Uri fileURI;
        private int version = 0;

        private bool hasCompletionUpdated = false;
        private Task<string> completionTask = null;

        //The command Handler processes keyboard input.
        internal RefactCompletionCommandHandler(IVsTextView textViewAdapter, ITextView textView, RefactCompletionHandlerProvider provider)
        {
            this.m_textView = textView;
            this.m_provider = provider;
            this.textViewAdapter = textViewAdapter;

            var topBuffer = textView.BufferGraph.TopBuffer;
            var projectionBuffer = topBuffer as IProjectionBufferBase;
            var typeName = topBuffer.GetType();
            ITextBuffer textBuffer = projectionBuffer != null ? projectionBuffer.SourceBuffers[0] : topBuffer;
            provider.documentFactory.TryGetTextDocument(textBuffer, out doc);

            if (doc != null && !string.IsNullOrEmpty(doc.FilePath))
            {
                this.fileURI = new Uri(doc.FilePath ?? throw new InvalidOperationException("Document file path is null."));
                this.filePath = this.fileURI.ToString();
            }
            else
            {
                Debug.WriteLine("doc.FilePath is null or empty.");
                this.filePath = string.Empty;
            }

            LoadLsp(this.filePath, doc);

            // Add the command to the command chain
            textViewAdapter.AddCommandFilter(this, out m_nextCommandHandler);
        }

        //Starts the refactlsp manually
        //Needed mostly for C/C++ 
        //some other languages don't start the refactlsp consistently but c/c++ appears to never start the lsp
        void LoadLsp(String file, ITextDocument doc){
            IComponentModel componentModel = (IComponentModel)m_provider.ServiceProvider.GetService(typeof(SComponentModel));
            ILanguageClientBroker clientBroker = componentModel.GetService<ILanguageClientBroker>();
            this.client = componentModel.GetExtensions<ILanguageClient>().ToList().Where((c) => c is RefactLanguageClient).FirstOrDefault() as RefactLanguageClient; ;
            if (!client.loaded){
                Task.Run(() => clientBroker.LoadAsync(new LanguageClientMetadata(new string[] { CodeRemoteContentDefinition.CodeRemoteBaseTypeName }), client));
            }
        }

        //Adds file to LSP
        async Task ConnectFileToLSP(){
            if (fileURI == null)
            {
                // Handle the case where fileURI is not initialized
                return;
            }

            if (!client.ContainsFile(filePath)){
                await client.AddFile(filePath, doc.TextBuffer.CurrentSnapshot.GetText());
            }else{
                version++;
                TextDocumentContentChangeEvent[] contentChanges = new TextDocumentContentChangeEvent[1];
                var snapshot = doc.TextBuffer.CurrentSnapshot;
                contentChanges[0] = new TextDocumentContentChangeEvent {
                    Text = snapshot.GetText(),
                    Range = new Range {
                        Start = new Position(0, 0),
                        End = new Position(snapshot.Lines.Count(), 0)
                    },
                    RangeLength = snapshot.Lines.Count()
                };
                await this.client.InvokeTextDocumentDidChangeAsync(fileURI, version, contentChanges);
            }
        }

        private MultilineGreyTextTagger GetTagger(){
            var key = typeof(MultilineGreyTextTagger);
            var props = m_textView.TextBuffer.Properties;
            if (props.ContainsProperty(key)){
                return props.GetProperty<MultilineGreyTextTagger>(key);
            }else{
                return null;
            }
        }

        //required by interface just boiler plate
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText){
            return m_nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public bool IsInline(int lineN){
            var text = m_textView.TextSnapshot.GetLineFromLineNumber(lineN).GetText();
            return !String.IsNullOrWhiteSpace(text);
        }

        //gets recommendations from LSP
        public async void GetLSPCompletions(){
           if (!General.Instance.PauseCompletion){
                SnapshotPoint? caretPoint = m_textView.Caret.Position.Point.GetPoint(textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);

                if (caretPoint.HasValue){
                    int lineN;
                    int characterN;
                    int res = textViewAdapter.GetCaretPos(out lineN, out characterN);

                    if (res == VSConstants.S_OK && RefactLanguageClient.Instance != null){
                        //Make sure caret is at the end of a line
                        String untrimLine = m_textView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(lineN).GetText();
                        if(characterN < untrimLine.Length){
                            String afterCaret = untrimLine.Substring(characterN);
                            String escapedSymbols = Regex.Escape(":(){ },.\"\';");

                            String pattern ="[\\s\\t\\n\\r" + escapedSymbols + "]*";
                            Match m = Regex.Match(afterCaret, pattern, RegexOptions.IgnoreCase);
                            if(!(m.Success && m.Index == 0 && m.Length == afterCaret.Length))
                                return;
                        }

                        await ConnectFileToLSP();

                        hasCompletionUpdated = false;
                        bool multiline = !IsInline(lineN);
                        if(completionTask == null || completionTask.IsCompleted){
                            completionTask = client.RefactCompletion(m_textView.TextBuffer.Properties, filePath, lineN, multiline ? 0 : characterN, multiline);
                            var s = await completionTask;
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            if (completionTask == null || completionTask.IsCompleted){
                                ShowRefactSuggestion(s, lineN, characterN);
                            }
                        }
                    }
                }
            }
        }

        //sends lsp reccomendations to grey text tagger to be dispalyed 
        public void ShowRefactSuggestion(String s, int lineN, int characterN)
        {
            if (!string.IsNullOrEmpty(s))
            {
                //the caret must be in a non-projection location 
                SnapshotPoint? caretPoint = m_textView.Caret.Position.Point.GetPoint(textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);
                if (!caretPoint.HasValue)
                {
                    return;
                }

                int newLineN;
                int newCharacterN;
                int resCaretPos = textViewAdapter.GetCaretPos(out newLineN, out newCharacterN);

                //double checks the cursor is still on the line the recommendation is for
                if (resCaretPos != VSConstants.S_OK || (lineN != newLineN) || (characterN != newCharacterN))
                {
                    return;
                }

                var tagger = GetTagger();
                if (tagger != null && s != null)
                {
                    tagger.SetSuggestion(s, IsInline(lineN), characterN);

                    // Ensure cursor is positioned correctly for multiline completions
                    if (s.Contains("\n"))
                    {
                        m_textView.Caret.MoveTo(new SnapshotPoint(m_textView.TextSnapshot, m_textView.TextSnapshot.Length));
                        m_textView.Caret.EnsureVisible();
                    }
                }
            }
        }



        //Used to detect when the user interacts with the intellisense popup
        void CheckSuggestionUpdate(uint nCmdID){
            switch (nCmdID){
                case ((uint)VSConstants.VSStd2KCmdID.UP):
                case ((uint)VSConstants.VSStd2KCmdID.DOWN):
                case ((uint)VSConstants.VSStd2KCmdID.PAGEUP):
                case ((uint)VSConstants.VSStd2KCmdID.PAGEDN):
                    if (m_provider.CompletionBroker.IsCompletionActive(m_textView)){
                        hasCompletionUpdated = true;
                    }

                    break;
                case ((uint)VSConstants.VSStd2KCmdID.TAB):
                case ((uint)VSConstants.VSStd2KCmdID.RETURN):
                    hasCompletionUpdated = false;
                    break;
            }
        }

        //Key input handler
        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut){
            //let the other handlers handle automation functions
            if (VsShellUtilities.IsInAutomationFunction(m_provider.ServiceProvider)){
                return m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }

            if (pguidCmdGroup == RefactPackage.CommandSet && nCmdID == TriggerCompletionCommand.CommandId)
            {
                GetLSPCompletions();
                return VSConstants.S_OK;
            }


            //check for a commit character
            if (!hasCompletionUpdated && nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB){

                var tagger = GetTagger();

                if (tagger != null){
                    if (tagger.IsSuggestionActive() && tagger.CompleteText()){                        
                        ClearCompletionSessions();
                        return VSConstants.S_OK;
                    }else{
                        tagger.ClearSuggestion();

                        // start the suggestions process again to see if suggestion left in between due to token limit
                        _ = Task.Run(() => GetLSPCompletions());
                    }
                }

            }else if(nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN || nCmdID == (uint)VSConstants.VSStd2KCmdID.CANCEL){
                var tagger = GetTagger();
                if (tagger != null){
                    tagger.ClearSuggestion();
                }
            }

            CheckSuggestionUpdate(nCmdID);
            //make a copy of this so we can look at it after forwarding some commands
            uint commandID = nCmdID;
            char typedChar = char.MinValue;

            //make sure the input is a char before getting it
            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR){
                typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
            }

            //pass along the command so the char is added to the buffer
            int retVal = m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            bool handled = false;

            //gets lsp completions on added character or deletions
            if (!typedChar.Equals(char.MinValue) || commandID == (uint)VSConstants.VSStd2KCmdID.RETURN){
                _ = Task.Run(() => GetLSPCompletions());
                handled = true;
            }else if (commandID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE || commandID == (uint)VSConstants.VSStd2KCmdID.DELETE){
                _ = Task.Run(()=>GetLSPCompletions());
                handled = true;
            }

            if (handled) return VSConstants.S_OK;
            return retVal;
        }

        //clears the intellisense popup window
        void ClearCompletionSessions(){
            m_provider.CompletionBroker.DismissAllSessions(m_textView);
        }

    }
}
