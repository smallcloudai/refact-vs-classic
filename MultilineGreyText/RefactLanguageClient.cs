using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using System.ComponentModel.Composition;
using System.Net.Sockets;
using System.Net;
using System.Xml.Xsl;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.VisualStudio.GraphModel.CodeSchema;
using System.Drawing;
using static System.Net.WebRequestMethods;
using stdole;

namespace RefactAI
{
    [ContentType("code")]
    [Export(typeof(ILanguageClient))]
    [RunOnContext(RunningContext.RunOnHost)]
    public class RefactLanguageClient : ILanguageClient, ILanguageClientCustomMessage2
    {
        public RefactLanguageClient()
        {
            Instance = this;
        }

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

        public event AsyncEventHandler<EventArgs> StartAsync;
        public event AsyncEventHandler<EventArgs> StopAsync;

        public string Name => "Refact Language Extension";

        public object InitializationOptions => null;

        public IEnumerable<string> FilesToWatch => null;

        public object MiddleLayer => RefactMiddleLayer.Instance;
        public object CustomMessageTarget => null;

        public bool ShowNotificationOnInitializeFailed => true;

        public IEnumerable<string> ConfigurationSections
        {
            get
            {
                yield return "";
            }
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
                return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
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

        public async void RefactCompletion(PropertyCollection props, String fileUri, int lineN, int character)
        {
            //Make sure lsp has finished loading
            if(this.Rpc == null)
            {
                return;
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

                var key = typeof(MultilineGreyTextTagger);
                if (props.ContainsProperty(key))
                {
                    var tagger = props.GetProperty<MultilineGreyTextTagger>(key);
                    tagger.SetSuggestion(suggestions[0]);
                }


            }
            catch (Exception e)
            {
                Debug.Write("Error " + e.ToString());
            }
        }

    internal class RefactMiddleLayer : ILanguageClientMiddleLayer
        {
            internal readonly static RefactMiddleLayer Instance = new RefactMiddleLayer();

            JsonRpc rpc = null;

            public bool CanHandle(string methodName)
            {
                return true;
            }

            public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification)
            {
                Task t = sendNotification(methodParam);

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
