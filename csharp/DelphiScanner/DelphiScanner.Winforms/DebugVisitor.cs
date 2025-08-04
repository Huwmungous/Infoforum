// DebugVisitor.cs - Use this to see what's actually being parsed
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;

namespace DelphiScanner.Winforms
{
    public class DebugVisitor : DelphiDfmBaseVisitor<object>
    {
        private int _depth = 0;

        public override object Visit(IParseTree tree)
        {
            string indent = new string(' ', _depth * 2);
            Console.WriteLine($"{indent}Visiting: {tree.GetType().Name}");

            if(tree is ParserRuleContext ctx)
            {
                Console.WriteLine($"{indent}  Text: '{ctx.GetText()}'");
                Console.WriteLine($"{indent}  Children count: {ctx.ChildCount}");
            }

            _depth++;
            var result = base.Visit(tree);
            _depth--;

            return result;
        }

        public override object VisitDfmFile(DelphiDfmParser.DfmFileContext context)
        {
            Console.WriteLine("[DEBUG] VisitDfmFile called");
            Console.WriteLine($"[DEBUG] DfmFile children count: {context.ChildCount}");

            for(int i = 0; i < context.ChildCount; i++)
            {
                var child = context.GetChild(i);
                Console.WriteLine($"[DEBUG] Child {i}: {child.GetType().Name} - '{child.GetText()}'");
            }

            return base.VisitChildren(context);
        }

        public override object VisitObjectDeclaration(DelphiDfmParser.ObjectDeclarationContext context)
        {
            Console.WriteLine($"[DEBUG] VisitObjectDeclaration called: {context.objectName.Text} : {context.className.Text}");
            return base.VisitChildren(context);
        }

        public override object VisitGenericProperty(DelphiDfmParser.GenericPropertyContext context)
        {
            Console.WriteLine($"[DEBUG] VisitGenericProperty called: {context.GetText()}");
            return base.VisitChildren(context);
        }
    }
}