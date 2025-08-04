using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DelphiScanner.Winforms
{
    // QueryUsageCollector.cs
    public class QueryUsageCollector : DelphiBaseVisitor<object>
    {
        private readonly string _formName;
        private readonly Dictionary<string, QueryInfo> _queries;

        public QueryUsageCollector(string formName, Dictionary<string, QueryInfo> queries)
        {
            _formName = formName;
            _queries = queries;
        }

        //public override object VisitFunctionDeclaration(DelphiParser.FunctionDeclarationContext context)
        //{
        //    _currentMethod = context.identifier()?.GetText() ?? "";
        //    return base.VisitFunctionDeclaration(context);
        //}

        //public override object VisitProcedureDeclaration(DelphiParser.ProcedureDeclarationContext context)
        //{
        //    _currentMethod = context.identifier()?.GetText() ?? "";
        //    return base.VisitProcedureDeclaration(context);
        //}

        //public override object VisitMemberAccess(DelphiParser.MemberAccessContext context)
        //{
        //    var objectName = context.primary()?.GetText();
        //    if(!string.IsNullOrEmpty(objectName))
        //    {
        //        var key = $"{_formName}.{objectName}";
        //        if(_queries.TryGetValue(key, out var query))
        //        {
        //            if(!string.IsNullOrEmpty(_currentMethod))
        //                query.Methods.Add(_currentMethod);
        //        }
        //    }

        //    return base.VisitMemberAccess(context);
        //}
    }


}
