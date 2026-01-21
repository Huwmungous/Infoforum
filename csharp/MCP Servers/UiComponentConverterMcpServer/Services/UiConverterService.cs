using System.Text;
using System.Text.RegularExpressions;

namespace UiComponentConverterMcpServer.Services;

public class UiConverterService(ILogger<UiConverterService> logger)
{
    private readonly ILogger<UiConverterService> _logger = logger;

    public DelphiForm ParseDelphiForm(string dfmContent)
    {
        try
        {
            var form = new DelphiForm
            {
                FormName = "UnknownForm",
                ClassName = "TForm"
            };

            var objectMatch = Regex.Match(dfmContent, @"object\s+(\w+):\s*(\w+)");
            if (objectMatch.Success)
            {
                form = form with
                {
                    FormName = objectMatch.Groups[1].Value,
                    ClassName = objectMatch.Groups[2].Value
                };
            }

            form.Components.AddRange(ParseComponents(dfmContent));

            return form;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Delphi form");
            throw;
        }
    }

    private static List<DelphiComponent> ParseComponents(string content)
    {
        var components = new List<DelphiComponent>();
        var objectPattern = @"object\s+(\w+):\s*(\w+)";
        var matches = Regex.Matches(content, objectPattern);

        foreach (Match match in matches)
        {
            var component = new DelphiComponent
            {
                Name = match.Groups[1].Value,
                ComponentType = match.Groups[2].Value
            };

            var eventPattern = $@"{component.Name}\.(\w+)\s*=";
            var eventMatches = Regex.Matches(content, eventPattern);
            foreach (Match eventMatch in eventMatches)
            {
                component.EventHandlers.Add(eventMatch.Groups[1].Value);
            }

            components.Add(component);
        }

        return components;
    }

    public ComponentAnalysis AnalyzeUiComponents(DelphiForm form)
    {
        var componentCounts = new Dictionary<string, int>();
        var events = new HashSet<string>();
        var dataBound = new List<string>();

        CountComponents(form.Components, componentCounts, events, dataBound);

        return new ComponentAnalysis
        {
            TotalComponents = form.Components.Count,
            ComponentTypeCounts = componentCounts,
            EventHandlers = [.. events],
            DataBoundComponents = dataBound
        };
    }

    private static void CountComponents(List<DelphiComponent> components, Dictionary<string, int> counts,
        HashSet<string> events, List<string> dataBound)
    {
        foreach (var component in components)
        {
            counts[component.ComponentType] = counts.GetValueOrDefault(component.ComponentType, 0) + 1;

            foreach (var handler in component.EventHandlers)
            {
                events.Add(handler);
            }

            if (component.ComponentType.StartsWith("TDB"))
            {
                dataBound.Add(component.Name);
            }

            CountComponents(component.Children, counts, events, dataBound);
        }
    }

    public GeneratedComponent MapToReact(DelphiForm form)
    {
        try
        {
            var component = new StringBuilder();
            var warnings = new List<string>();

            component.AppendLine("import React, { useState, useEffect } from 'react';");
            component.AppendLine($"import './{form.FormName}.css';");
            component.AppendLine();
            component.AppendLine($"export const {form.FormName}: React.FC = () => {{");
            component.AppendLine("  const [formData, setFormData] = useState({});");
            component.AppendLine();

            foreach (var comp in form.Components)
            {
                var reactComponent = ConvertToReactComponent(comp);
                component.AppendLine($"  // {comp.ComponentType}: {comp.Name}");
                component.AppendLine($"  {reactComponent}");

                if (comp.ComponentType.StartsWith("TDB"))
                {
                    warnings.Add($"{comp.Name} is data-bound - needs API integration");
                }
            }

            component.AppendLine();
            component.AppendLine("  return (");
            component.AppendLine("    <div className=\"form-container\">");
            component.AppendLine("      {/* Add components here */}");
            component.AppendLine("    </div>");
            component.AppendLine("  );");
            component.AppendLine("};");

            var css = GenerateCss();

            return new GeneratedComponent
            {
                Success = true,
                ComponentCode = component.ToString(),
                StyleCode = css,
                Message = "React component generated",
                Warnings = [.. warnings]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating React component");
            return new GeneratedComponent
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public GeneratedComponent MapToAngular(DelphiForm form)
    {
        try
        {
            var component = new StringBuilder();
            var warnings = new List<string>();

            component.AppendLine("import { Component, OnInit } from '@angular/core';");
            component.AppendLine();
            component.AppendLine("@Component({");
            component.AppendLine($"  selector: 'app-{form.FormName.ToLower()}',");
            component.AppendLine($"  templateUrl: './{form.FormName.ToLower()}.component.html',");
            component.AppendLine($"  styleUrls: ['./{form.FormName.ToLower()}.component.css']");
            component.AppendLine("})");
            component.AppendLine($"export class {form.FormName}Component implements OnInit {{");
            component.AppendLine("  formData: any = {};");
            component.AppendLine();
            component.AppendLine("  constructor() { }");
            component.AppendLine();
            component.AppendLine("  ngOnInit(): void {");
            component.AppendLine("    // Initialize form data");
            component.AppendLine("  }");

            foreach (var comp in form.Components)
            {
                foreach (var handler in comp.EventHandlers)
                {
                    component.AppendLine();
                    component.AppendLine($"  on{comp.Name}{handler}() {{");
                    component.AppendLine($"    // Handle {handler} for {comp.Name}");
                    component.AppendLine("  }");
                }

                if (comp.ComponentType.StartsWith("TDB"))
                {
                    warnings.Add($"{comp.Name} is data-bound - needs service integration");
                }
            }

            component.AppendLine("}");

            var css = GenerateCss();

            return new GeneratedComponent
            {
                Success = true,
                ComponentCode = component.ToString(),
                StyleCode = css,
                Message = "Angular component generated",
                Warnings = [.. warnings]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Angular component");
            return new GeneratedComponent
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public GeneratedComponent MapToBlazor(DelphiForm form)
    {
        try
        {
            var component = new StringBuilder();
            var warnings = new List<string>();

            component.AppendLine("@page \"/" + form.FormName.ToLower() + "\"");
            component.AppendLine($"@using {form.FormName}");
            component.AppendLine();
            component.AppendLine("<div class=\"form-container\">");
            component.AppendLine($"    <h3>{form.FormName}</h3>");

            foreach (var comp in form.Components)
            {
                var blazorComponent = ConvertToBlazorComponent(comp);
                component.AppendLine($"    {blazorComponent}");

                if (comp.ComponentType.StartsWith("TDB"))
                {
                    warnings.Add($"{comp.Name} is data-bound - needs data service");
                }
            }

            component.AppendLine("</div>");
            component.AppendLine();
            component.AppendLine("@code {");
            component.AppendLine("    private class FormModel");
            component.AppendLine("    {");
            component.AppendLine("        // Add properties here");
            component.AppendLine("    }");
            component.AppendLine();
            component.AppendLine("    private FormModel formData = new();");
            component.AppendLine();

            foreach (var comp in form.Components)
            {
                foreach (var handler in comp.EventHandlers)
                {
                    component.AppendLine($"    private void On{comp.Name}{handler}()");
                    component.AppendLine("    {");
                    component.AppendLine($"        // Handle {handler} for {comp.Name}");
                    component.AppendLine("    }");
                    component.AppendLine();
                }
            }

            component.AppendLine("}");

            var css = GenerateCss();

            return new GeneratedComponent
            {
                Success = true,
                ComponentCode = component.ToString(),
                StyleCode = css,
                Message = "Blazor component generated",
                Warnings = [.. warnings]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Blazor component");
            return new GeneratedComponent
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public List<string> ExtractEventHandlers(DelphiForm form)
    {
        var handlers = new HashSet<string>();
        ExtractHandlersRecursive(form.Components, handlers);
        return [.. handlers];
    }

    private static void ExtractHandlersRecursive(List<DelphiComponent> components, HashSet<string> handlers)
    {
        foreach (var component in components)
        {
            foreach (var handler in component.EventHandlers)
            {
                handlers.Add($"{component.Name}.{handler}");
            }
            ExtractHandlersRecursive(component.Children, handlers);
        }
    }

    public string GenerateCssLayout()
    {
        return GenerateCss();
    }

    private static string GenerateCss()
    {
        var css = new StringBuilder();
        css.AppendLine(".form-container {");
        css.AppendLine("  padding: 20px;");
        css.AppendLine("  max-width: 1200px;");
        css.AppendLine("  margin: 0 auto;");
        css.AppendLine("}");
        css.AppendLine();
        css.AppendLine(".form-field {");
        css.AppendLine("  margin-bottom: 15px;");
        css.AppendLine("}");
        css.AppendLine();
        css.AppendLine(".form-label {");
        css.AppendLine("  display: block;");
        css.AppendLine("  margin-bottom: 5px;");
        css.AppendLine("  font-weight: bold;");
        css.AppendLine("}");
        css.AppendLine();
        css.AppendLine(".form-input {");
        css.AppendLine("  width: 100%;");
        css.AppendLine("  padding: 8px;");
        css.AppendLine("  border: 1px solid #ccc;");
        css.AppendLine("  border-radius: 4px;");
        css.AppendLine("}");
        return css.ToString();
    }

    public string CreateStateModel(DelphiForm form)
    {
        var model = new StringBuilder();
        model.AppendLine("// State model for " + form.FormName);
        model.AppendLine("export interface " + form.FormName + "State {");

        foreach (var component in form.Components)
        {
            if (component.ComponentType.Contains("Edit") || component.ComponentType.Contains("Memo"))
            {
                model.AppendLine($"  {component.Name}: string;");
            }
            else if (component.ComponentType.Contains("CheckBox"))
            {
                model.AppendLine($"  {component.Name}: boolean;");
            }
        }

        model.AppendLine("}");
        return model.ToString();
    }

    private static string ConvertToReactComponent(DelphiComponent component)
    {
        return component.ComponentType switch
        {
            var t when t.Contains("Edit") => $"<input type=\"text\" name=\"{component.Name}\" />",
            var t when t.Contains("Button") => $"<button onClick={{handle{component.Name}Click}}>{component.Name}</button>",
            var t when t.Contains("Label") => $"<label>{component.Name}</label>",
            var t when t.Contains("CheckBox") => $"<input type=\"checkbox\" name=\"{component.Name}\" />",
            var t when t.Contains("Grid") => $"<table className=\"grid-{component.Name}\">{{/* Grid */}}</table>",
            _ => $"<div>{{/* {component.ComponentType}: {component.Name} */}}</div>"
        };
    }

    private static string ConvertToBlazorComponent(DelphiComponent component)
    {
        return component.ComponentType switch
        {
            var t when t.Contains("Edit") => $"<input type=\"text\" @bind=\"formData.{component.Name}\" />",
            var t when t.Contains("Button") => $"<button @onclick=\"On{component.Name}Click\">{component.Name}</button>",
            var t when t.Contains("Label") => $"<label>{component.Name}</label>",
            var t when t.Contains("CheckBox") => $"<input type=\"checkbox\" @bind=\"formData.{component.Name}\" />",
            var t when t.Contains("Grid") => $"<table>{{/* Grid: {component.Name} */}}</table>",
            _ => $"<!-- {component.ComponentType}: {component.Name} -->"
        };
    }
}