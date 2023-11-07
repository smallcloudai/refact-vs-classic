using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using System.Windows.Controls;
using Microsoft.Build.Framework.XamlTypes;

namespace RefactAI
{
    [ContentType("any")]//CodeRemoteContentDefinition.CodeRemoteBaseTypeName
    [Export(typeof(ILanguageClient))]
    [RunOnContext(RunningContext.RunOnHost)]
    public class RefactLanguageClient : ILanguageClient, ILanguageClientCustomMessage2
    {
        private Connection c;

        internal static RefactLanguageClient Instance
        {
            get;
            set;
        }

        internal JsonRpc Rpc
        {
            get;
            set;
        }

        public bool loaded = false;

        public event AsyncEventHandler<EventArgs> StartAsync;
        public event AsyncEventHandler<EventArgs> StopAsync;

        public string Name => "Refact Language Extension";

        public object InitializationOptions => null;

        public IEnumerable<string> FilesToWatch => null;

        public object MiddleLayer => RefactMiddleLayer.Instance;
        public object CustomMessageTarget => null;

        public bool ShowNotificationOnInitializeFailed => true;

        internal HashSet<String> files;
        public RefactLanguageClient()
        {
            Instance = this;
            files = new HashSet<string>();
        }

        public IEnumerable<string> ConfigurationSections
        {
            get
            {
                yield return "";
            }
        }
        
        public async void AddFile(String filePath, String text)
        {
            while (Rpc == null) await Task.Delay(1);

            if (ContainsFile(filePath))
            {
                return;
            }

            files.Add(filePath);

            var openParam = new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = new Uri(filePath),
                    LanguageId = filePath.Substring(filePath.LastIndexOf(".") + 1),
                    Version = 0,
                    Text = text
                }
            };

            try
            {
                await Rpc.NotifyWithParameterObjectAsync("textDocument/didChange", openParam);
            }
            catch (Exception e)
            {
                Debug.Write("InvokeTextDocumentDidChangeAsync Server Exception " + e.ToString());
            }
        }
        public bool ContainsFile(String file)
        {
            return files.Contains(file);
        }

        public async Task<Connection> ActivateAsync(CancellationToken token)
        {
            ProcessStartInfo info = new ProcessStartInfo();

            info.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "RefactLSP", @"refact-lsp.exe");

            info.Arguments = GetArgs();
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;

            Process process = new Process();
            process.StartInfo = info;

            if (process.Start())
            {
                this.c = new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
                return c;
            }

            return null;
        }

        String GetArgs()
        {
            String args = "";
            if (General.Instance.TelemetryBasic)
            {
                args += "--basic-telemetry ";
            }

            if (General.Instance.TelemetryCodeSnippets)
            {
                args += "--snippet-telemetry ";
            }

            args += "--address-url " + (String.IsNullOrWhiteSpace(General.Instance.AddressURL) ? "Refact" : General.Instance.AddressURL) + " ";
            args += "--api-key " + (String.IsNullOrWhiteSpace(General.Instance.APIKey) ? "ZZZWWW" : General.Instance.APIKey) + " ";

            return args + "--http-port 8001 --lsp-stdin-stdout 1 --logs-stderr";
        }
        public async Task OnLoadedAsync()
        {
            if (StartAsync != null)
            {
                loaded = true;
                await StartAsync.InvokeAsync(this, EventArgs.Empty);
            }
        }

        public async Task StopServerAsync()
        {
            if (StopAsync != null)
            {
                await StopAsync.InvokeAsync(this, EventArgs.Empty);
            }
        }

        public Task OnServerInitializedAsync()
        {
            return Task.CompletedTask;
        }

        public Task AttachForCustomMessageAsync(JsonRpc rpc)
        {
            this.Rpc = rpc;
            return Task.CompletedTask;
        }

        public Task<InitializationFailureContext> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
        {
            string message = "Oh no! Refact Language Client failed to activate, now we can't test LSP! :(";
            string exception = initializationState.InitializationException?.ToString() ?? string.Empty;
            message = $"{message}\n {exception}";

            var failureContext = new InitializationFailureContext()
            {
                FailureMessage = message,
            };

            return Task.FromResult(failureContext);
        }

        public async void InvokeTextDocumentDidChangeAsync(Uri fileURI, int version, TextDocumentContentChangeEvent[] contentChanges)
        {
            if (Rpc != null && ContainsFile(fileURI.ToString()))
            {
                var changesParam = new DidChangeTextDocumentParams
                {
                    ContentChanges = contentChanges,
                    TextDocument = new VersionedTextDocumentIdentifier
                    {
                        Version = version,
                        Uri = fileURI,
                    }
                };

                try
                {
                    await Rpc.NotifyWithParameterObjectAsync("textDocument/didChange", changesParam);
                }catch(Exception e)
                {
                    Debug.Write("InvokeTextDocumentDidChangeAsync Server Exception " + e.ToString());
                }
            }
        }
        public async Task<string> RefactCompletion(PropertyCollection props, String fileUri, int lineN, int character)
        {
            //Make sure lsp has finished loading
            if(this.Rpc == null)
            {
                return null;
            }

            //catching server errors
            try
            {
                var argObj2 = new
                {
                    text_document_position = new
                    {
                        textDocument = new { uri = fileUri },
                        position = new
                        {
                            line = lineN,
                            character = character 
                        },
                    },
                    parameters = new { max_new_tokens = 50, temperature = 0.2f },
                    multiline = true,
                    textDocument = new { uri = fileUri},
                    position = new
                    {
                        line = lineN,
                        character = character
                    }
                };
                
                var res = await this.Rpc.InvokeWithParameterObjectAsync<JToken>("refact/getCompletions", argObj2);
                List<String> suggestions = new List<String>();
                foreach (var s in res["choices"])
                {
                    suggestions.Add(s["code_completion"].ToString());
                }

                return suggestions[0];
            }
            catch (Exception e)
            {
                Debug.Write("Error " + e.ToString());
                return null;
            }
        }

    internal class RefactMiddleLayer : ILanguageClientMiddleLayer
        {
            internal readonly static RefactMiddleLayer Instance = new RefactMiddleLayer();

            public bool CanHandle(string methodName)
            {
                return true;
            }

            public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification)
            {
                Task t = sendNotification(methodParam);
                if (methodName == "textDocument/didOpen")
                {
                   RefactLanguageClient.Instance.files.Add(methodParam["textDocument"]["uri"].ToString());
                }
                return t;
            }
            public async Task<JToken> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken>> sendRequest)
            {
                var result = await sendRequest(methodParam);
                if(methodName == "textDocument/completion")
                {
                    return JToken.Parse("[]");
                }
                else
                {
                    return result;
                }
            }
        }
    }
}
