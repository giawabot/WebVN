using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebVN.Editor;
using WebVN.Editor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<ICsvScriptImportService, CsvScriptImportService>();
builder.Services.AddScoped<IBrowserProjectStorage, BrowserProjectStorage>();
builder.Services.AddScoped<IProjectPackageService, BrowserProjectPackageService>();
builder.Services.AddScoped<EditorState>();

await builder.Build().RunAsync();
