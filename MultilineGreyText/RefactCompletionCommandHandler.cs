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
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace RefactAI
{
    internal class RefactCompletionCommandHandler : IOleCommandTarget
    {
        //LanguageClientMetadata is needed to manually load LSP
        private class LanguageClientMetadata : ILanguageClientMetadata
        {
            public LanguageClientMetadata(string[] contentTypes, string clientName = null)
            {
                this.ContentTypes = contentTypes;
                this.ClientName = clientName;
            }

            public string ClientName { get; }

            public IEnumerable<string> ContentTypes { get; }
        }

        private IOleCommandTarget m_nextCommandHandler;
        private ITextView m_textView;
        private RefactCompletionHandlerProvider m_provider;
        private ICompletionSession m_session;
        private IVsTextView textViewAdapter;
        private String filePath;
        private Uri fileURI;
        private int version = 0;
        private RefactLanguageClient client = null;

        internal RefactCompletionCommandHandler(IVsTextView textViewAdapter, ITextView textView, RefactCompletionHandlerProvider provider)
        {
            this.m_textView = textView;
            this.m_provider = provider;
            this.textViewAdapter = textViewAdapter;

            ITextDocument doc;

            var topBuffer = textView.BufferGraph.TopBuffer;
            var projectionBuffer = topBuffer as IProjectionBufferBase;
            var typeName = topBuffer.GetType();
            ITextBuffer textBuffer = projectionBuffer != null ? projectionBuffer.SourceBuffers[0] : topBuffer;
            provider.documentFactory.TryGetTextDocument(textBuffer, out doc);
            this.fileURI = new Uri(doc.FilePath);
            this.filePath = this.fileURI.ToString();
            LoadLsp(this.filePath, doc);

            //add the command to the command chain
            textViewAdapter.AddCommandFilter(this, out m_nextCommandHandler);            
        }

        //Needed purely for C/C++
        void LoadLsp(String file, ITextDocument doc)
        {
            IComponentModel componentModel = (IComponentModel)m_provider.ServiceProvider.GetService(typeof(SComponentModel));
            ILanguageClientBroker clientBroker = componentModel.GetService<ILanguageClientBroker>();
            this.client = componentModel.GetExtensions<ILanguageClient>().ToList().Where((c) => c is RefactLanguageClient).FirstOrDefault() as RefactLanguageClient; ;
            if (!client.loaded)
            {
                Task.Run(() => clientBroker.LoadAsync(new LanguageClientMetadata(new string[] { CodeRemoteContentDefinition.CodeRemoteBaseTypeName }), client));
            }

            if (!client.ContainsFile(filePath))
            {
                client.AddFile(filePath, doc.TextBuffer.CurrentSnapshot.GetText());
                ((ITextBuffer2)doc.TextBuffer).ChangedHighPriority += ChangeEvent;
            }
        }

        private void ChangeEvent(object sender, TextContentChangedEventArgs args)
        {
            version++;

            // The changes in textChanges all apply to the same original document state. The changes sent to the
            // server are expected to apply to the state as of the previous change in the list. To prevent the
            // changes from affecting one another we reverse the list.
            TextDocumentContentChangeEvent[] contentChanges = args.Changes.Reverse().Select<ITextChange, TextDocumentContentChangeEvent>(change =>
            {
                int startLine, startColumn;
                textViewAdapter.GetLineAndColumn(change.OldSpan.Start, out startLine, out startColumn);
                int endLine, endColumn;
                textViewAdapter.GetLineAndColumn(change.OldSpan.Start, out endLine, out endColumn);
                
                return new TextDocumentContentChangeEvent
                {
                    Text = change.NewText,
                    Range = new Range{
                        Start = new Position(startLine, startColumn),
                        End = new Position(endLine, endColumn)
                    },
                    RangeLength = change.OldSpan.Length
                };
            }).ToArray();

            contentChanges[0].Text = m_textView.TextBuffer.CurrentSnapshot.GetText();
            this.client.InvokeTextDocumentDidChangeAsync(fileURI, version, contentChanges);
        }
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            return m_nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public void GetLSPCompletions()
        {
           if (!General.Instance.PauseCompletion)
            {
                SnapshotPoint? caretPoint = m_textView.Caret.Position.Point.GetPoint(textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);

                if (caretPoint.HasValue)
                {
                    int lineN;
                    int characterN;
                    int res = textViewAdapter.GetCaretPos(out lineN, out characterN);

                    if (res == VSConstants.S_OK && RefactLanguageClient.Instance != null)
                    {
                        RefactLanguageClient.Instance.RefactCompletion(m_textView.TextBuffer.Properties, filePath, lineN, characterN);
                    }
                }
            }
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (VsShellUtilities.IsInAutomationFunction(m_provider.ServiceProvider))
            {
                return m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }

            //check for a commit character
            if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN ||
                nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB)
            {
                var key = typeof(MultilineGreyTextTagger);
                var props = m_textView.TextBuffer.Properties;
                if (props.ContainsProperty(key))
                {
                    var tagger = props.GetProperty<MultilineGreyTextTagger>(key);
                    if (tagger.IsSuggestionActive() && tagger.CompleteText())
                    {
                        if (m_session != null)
                        {
                            m_session.Dismiss();
                        }

                        return VSConstants.S_OK;
                    }
                    else
                    {
                        tagger.ClearSuggestion();
                    }
                }
            }

            //make a copy of this so we can look at it after forwarding some commands
            uint commandID = nCmdID;
            char typedChar = char.MinValue;

            //make sure the input is a char before getting it
            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
            {
                typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
            }

            //check for a selection
            if (m_session != null && !m_session.IsDismissed &&
                (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN ||
                nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB ||
                (char.IsWhiteSpace(typedChar) || char.IsPunctuation(typedChar))))
            {
                //if the selection is fully selected, commit the current session
                if (m_session.SelectedCompletionSet.SelectionStatus.IsSelected)
                {
                    m_session.Commit();
                    //also, don't add the character to the buffer
                    return VSConstants.S_OK;
                }
                else
                {
                    //if there is no selection, dismiss the session
                    m_session.Dismiss();
                }
            }

            //pass along the command so the char is added to the buffer
            int retVal = m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            bool handled = false;
            if (!typedChar.Equals(char.MinValue) && char.IsLetterOrDigit(typedChar))
            {
                if (m_session == null || m_session.IsDismissed) // If there is no active session, bring up completion
                {
                    this.TriggerCompletion();
                }

                if (m_session != null && !m_session.IsDismissed)
                {
                    m_session.Filter();
                }

                GetLSPCompletions();
                handled = true;
            }
            //redo the filter if there is a deletion
            else if (commandID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE ||
                commandID == (uint)VSConstants.VSStd2KCmdID.DELETE)
            {
                if(m_session != null && !m_session.IsDismissed)
                {
                    m_session.Filter();
                }

                GetLSPCompletions();
                handled = true;
            }

            if (handled) return VSConstants.S_OK;
            return retVal;
        }

        private bool TriggerCompletion()
        {
            //the caret must be in a non-projection location 
            SnapshotPoint? caretPoint = m_textView.Caret.Position.Point.GetPoint(textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);
            if (!caretPoint.HasValue)
            {
                return false;
            }

            m_session = m_provider.CompletionBroker.CreateCompletionSession(m_textView,caretPoint.Value.Snapshot.CreateTrackingPoint(caretPoint.Value.Position, PointTrackingMode.Positive),true);

            //subscribe to the Dismissed event on the session 
            m_session.Dismissed += this.OnSessionDismissed;
            m_session.Start();

            return true;
        }

        private void OnSessionDismissed(object sender, EventArgs e)
        {
            m_session.Dismissed -= this.OnSessionDismissed;
            m_session = null;
        }
    }
}
