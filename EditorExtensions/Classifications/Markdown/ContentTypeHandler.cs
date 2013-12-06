﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using Microsoft.Html.Core;
using Microsoft.Html.Editor;
using Microsoft.Html.Editor.ContainedLanguage;
using Microsoft.VisualStudio.Utilities;

namespace MadsKristensen.EditorExtensions.Classifications.Markdown
{
    [Export(typeof(IContentTypeHandlerProvider))]
    [ContentType(MarkdownContentTypeDefinition.MarkdownContentType)]
    public class MarkdownContentTypeHandlerProvider : IContentTypeHandlerProvider
    {
        [Import]
        public IContentTypeRegistryService ContentTypeRegistry { get; set; }

        public IContentTypeHandler GetContentTypeHandler() { return new MarkdownContentTypeHandler(ContentTypeRegistry); }
    }

    public class MarkdownContentTypeHandler : HtmlContentTypeHandler
    {
        static readonly Func<HtmlContentTypeHandler, List<LanguageBlockHandler>> GetLanguageBlockHandlerList =
            (Func<HtmlContentTypeHandler, List<LanguageBlockHandler>>)
            Delegate.CreateDelegate(
                typeof(Func<HtmlContentTypeHandler, List<LanguageBlockHandler>>),
                typeof(HtmlContentTypeHandler).GetProperty("LanguageBlockHandlers", BindingFlags.NonPublic | BindingFlags.Instance).GetMethod
            );

        readonly IContentTypeRegistryService contentTypeRegistry;
        public MarkdownContentTypeHandler(IContentTypeRegistryService contentTypeRegistry)
        {
            this.contentTypeRegistry = contentTypeRegistry;
        }

        public override ArtifactCollection CreateArtifactCollection() { return new MarkdownCodeArtifactCollection(new MarkdownCodeArtifactProcessor()); }

        protected override void CreateBlockHandlers()
        {
            base.CreateBlockHandlers();
            GetLanguageBlockHandlerList(this).Add(new CodeBlockBlockHandler(EditorTree, contentTypeRegistry));
        }

        public override void UpdateContainedLanguageBuffers()
        {
            // TODO: Call RemoveSpans() on each created LanguageProjectionBuffer iff IsRegenerationNeeded()
            base.UpdateContainedLanguageBuffers();
        }
    }

    class CodeBlockBlockHandler : ArtifactBasedBlockHandler
    {
        readonly IContentTypeRegistryService contentTypeRegistry;
        public CodeBlockBlockHandler(HtmlEditorTree tree, IContentTypeRegistryService contentTypeRegistry)
            : base(tree, contentTypeRegistry.GetContentType("code"))
        {
            this.contentTypeRegistry = contentTypeRegistry;
        }
        protected override BufferGenerator CreateBufferGenerator()
        {
            return new MarkdownBufferGenerator(EditorTree, LanguageBlocks, contentTypeRegistry);
        }
        public override IContentType GetContentTypeOfLocation(int position)
        {
            LanguageBlock block = this.GetLanguageBlockOfLocation(position);
            if (block == null) return null;
            var alb = block as ArtifactLanguageBlock;
            if (alb == null)
                return contentTypeRegistry.GetContentType("text");

            return alb.ContentType ?? contentTypeRegistry.GetContentType("code");
        }

        protected override void BuildLanguageBlockCollection()
        {
            LanguageBlocks.Clear();
            foreach (var artifact in EditorTree.RootNode.Tree.ArtifactCollection.OfType<CodeLineArtifact>())
            {
                if (artifact.BlockInfo == null)
                    continue;
                var contentType = contentTypeRegistry.FromFriendlyName(artifact.BlockInfo.Language);
                if (contentType != null)
                    LanguageBlocks.AddBlock(new ArtifactLanguageBlock(artifact, contentType));
            }
            LanguageBlocks.SortByPosition();
        }
    }

    class ArtifactLanguageBlock : LanguageBlock
    {
        public ArtifactLanguageBlock(CodeLineArtifact a, IContentType contentType)
            : base(a)
        {
            ContentType = contentType;
        }
        public IContentType ContentType { get; private set; }
    }
}
