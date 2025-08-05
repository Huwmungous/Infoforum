using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic; 

namespace DelphiScanner.Winforms
{ 
    public class DelphiVisitor : DelphiBaseVisitor<object>
    {
        public string Form = string.Empty;
        public Dictionary<string, QueryInfo> QueryMap = [];

        private string _currentMethodName = string.Empty; 

        public override object VisitProcDecl(DelphiParser.ProcDeclContext context)
        {
            // Get the method name text
            _currentMethodName = context.procDeclHeading()?.GetText() ?? "?";
            Console.WriteLine($"PROC DECL: {_currentMethodName}");

            return base.VisitChildren(context); // MUST be called to continue visiting the body
        }

        //public override object VisitMethodDecl(DelphiParser.MethodDeclContext context)
        //{
        //    var txt = context.GetText();
        //    return base.VisitChildren(context);
        //}

        //public override object VisitMethodDeclHeading(DelphiParser.MethodDeclHeadingContext context)
        //{
        //    var txt = context.GetText();
        //    return base.VisitChildren(context);
        //}

        //public override object VisitMethodKey(DelphiParser.MethodKeyContext context)
        //{
        //    var txt = context.GetText();
        //    return base.VisitChildren(context);
        //}

        public override object VisitMethodName(DelphiParser.MethodNameContext context)
        {
            _currentMethodName = context.GetText();
            return base.VisitChildren(context);
        }

        public override object VisitCompoundStatement(DelphiParser.CompoundStatementContext context)
        { 
            RecordQueryUsage(context.GetText());

            return base.VisitChildren(context);
        }

        public override object VisitMethodBody(DelphiParser.MethodBodyContext context)
        {
            RecordQueryUsage(context.GetText());

            return base.VisitChildren(context);
        }

        private void RecordQueryUsage(string body)
        {
            foreach(var key in QueryMap.Keys)
            {
                if(body.Contains(key, StringComparison.Ordinal))
                {
                    var query = QueryMap[key];
                    if(!string.IsNullOrEmpty(_currentMethodName) &&
                        !query.Usage.Contains(_currentMethodName))
                    {
                        query.Usage.Add(_currentMethodName);
                    }
                }
            }
        }


        // Optional: log all rule visits for debugging
        // public override object VisitChildren(IRuleNode node)
        // {
        //     Console.WriteLine($"Visiting: {node.GetType().Name} -> {node}");
        //     return base.VisitChildren(node);
        // }
    }
}
