using Markdig;
using Serilog;

namespace irHub.Helpers;

internal struct ReleaseNotesHelper
{
    internal static string GenerateReleaseNotes(string releaseNotes)
    {
        Log.Information("Generating releasenotes HTML..");
        
        const string css = "<link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/github-markdown-css/5.1.0/github-markdown-dark.min.css'>";
        string html = Markdown.ToHtml(releaseNotes);
        return $@"
        <html>
        <head>
            {css}
            <style>
                body {{
                    font-family: Arial, sans-serif;
                    background-color: #0d1117;
                    color: #c9d1d9;
                    display: flex;
                    justify-content: center;
                    padding: 18px;
                    overflow-x: hidden;
                    overflow-y: auto;
                }}
                .markdown-body {{
                    max-width: 800px;
                    background: #161b22;
                    padding: 20px;
                    border-radius: 8px;
                    box-shadow: 0px 0px 10px rgba(255, 255, 255, 0.1);
                    overflow-y: auto;
                    max-height: 90vh;
                }}
                h1, h2, h3 {{
                    border-bottom: 1px solid #30363d;
                    padding-bottom: 5px;
                }}
                blockquote {{
                    margin: 0;
                    padding: 10px;
                    border-left: 5px solid #8b949e;
                    background: #161b22;
                    font-style: italic;
                    color: #8b949e;
                }}
                pre {{
                    background: #0d1117;
                    padding: 10px;
                    border-radius: 6px;
                    overflow-x: auto;
                }}
                ::-webkit-scrollbar {{
                    width: 10px;
                }}

                ::-webkit-scrollbar-track {{
                    background: #161b22;
                    border-radius: 10px;
                }}

                ::-webkit-scrollbar-thumb {{
                    background: #30363d;
                    border-radius: 10px;
                }}

                ::-webkit-scrollbar-thumb:hover {{
                    background: #484f58;
                }}
            </style>
        </head>
        <body>
            <article class='markdown-body'>
                {html}
            </article>
        </body>
        </html>";
    }
}