namespace _24Days.Core.Services;

using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Umbraco.Cms.Core.PropertyEditors;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Extensions;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using _24Days.Core.Helpers;

public interface IBackOfficePreviewService
{
    Task<string> GetMarkupForBlock(
        BlockItemData blockData,
        ControllerContext controllerContext);
}

public sealed class BackOfficePreviewService : IBackOfficePreviewService
{
    private readonly BlockEditorConverter _blockEditorConverter;

    private readonly ITempDataProvider _tempDataProvider;

    private readonly ITypeFinder _typeFinder;

    private readonly IPublishedValueFallback _publishedValueFallback;

    private readonly IViewComponentHelperWrapper _viewComponentHelperWrapper;

    private readonly IViewComponentSelector _viewComponentSelector;

    private readonly IRazorViewEngine _razorViewEngine;

    public BackOfficePreviewService(
        BlockEditorConverter blockEditorConverter,
        ITempDataProvider tempDataProvider,
        ITypeFinder typeFinder,
        IPublishedValueFallback publishedValueFallback,
        IViewComponentHelperWrapper viewComponentHelperWrapper,
        IViewComponentSelector viewComponentSelector,
        IRazorViewEngine razorViewEngine)
    {
        _blockEditorConverter = blockEditorConverter;
        _tempDataProvider = tempDataProvider;
        _typeFinder = typeFinder;
        _publishedValueFallback = publishedValueFallback;
        _viewComponentHelperWrapper = viewComponentHelperWrapper;
        _viewComponentSelector = viewComponentSelector;
        _razorViewEngine = razorViewEngine;
    }

    public async Task<string> GetMarkupForBlock(
        BlockItemData blockData,
        ControllerContext controllerContext)
    {
        var element = _blockEditorConverter.ConvertToElement(blockData, PropertyCacheLevel.None, true);
        if (element == null)
        {
            throw new InvalidOperationException($"Unable to find Element {blockData.ContentTypeAlias}");
        }

        var blockType = _typeFinder.FindClassesWithAttribute<PublishedModelAttribute>().FirstOrDefault(
            x => x.GetCustomAttribute<PublishedModelAttribute>(false)?.ContentTypeAlias == element.ContentType.Alias);

        if (blockType == null)
        {
            throw new InvalidOperationException($"Unable to find BlockType {element.ContentType.Alias}");
        }

        // create instance of the models builder type based from the element
        var blockInstance = Activator.CreateInstance(blockType, element, _publishedValueFallback);

        // get a generic block list item type based on the models builder type
        var blockListItemType = typeof(BlockListItem<>).MakeGenericType(blockType);

        // create instance of the block list item
        // if you want to use settings this will need to be changed.
        var blockListItem = (BlockListItem?)Activator.CreateInstance(
            blockListItemType,
            blockData.Udi,
            blockInstance,
            null,
            null);

        var viewData =
            new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = blockListItem
            };
        var contentAlias = element.ContentType.Alias.ToFirstUpper();

        var viewComponent = _viewComponentSelector.SelectComponent(contentAlias);
        if (viewComponent != null)
        {
            return await GetMarkupFromViewComponent(controllerContext, viewData, viewComponent, blockListItem);
        }

        return await GetMarkFromPartial(controllerContext, viewData, contentAlias);
    }

    private async Task<string> GetMarkFromPartial(ControllerContext controllerContext,
        ViewDataDictionary viewData,
        string contentAlias)
    {
        var partialName = $"/Views/Partials/blocklist/Components/{contentAlias}.cshtml";


        var actionContext = new ActionContext(controllerContext.HttpContext, new RouteData(), new ActionDescriptor());

        await using var sw = new StringWriter();
        var viewResult = _razorViewEngine.GetView(partialName, partialName, false);

        if (viewResult?.View != null)
        {
            var viewContext = new ViewContext(actionContext, viewResult.View, viewData,
                new TempDataDictionary(actionContext.HttpContext, _tempDataProvider), sw, new HtmlHelperOptions());
            await viewResult.View.RenderAsync(viewContext);
        }

        return sw.ToString();
    }

    private async Task<string> GetMarkupFromViewComponent(ControllerContext controllerContext,
        ViewDataDictionary viewData,
        ViewComponentDescriptor viewComponent,
        IBlockReference? blockListItem)
    {
        await using var sw = new StringWriter();
        var viewContext = new ViewContext(
            controllerContext,
            new FakeView(),
            viewData,
            new TempDataDictionary(controllerContext.HttpContext, _tempDataProvider),
            sw,
            new HtmlHelperOptions());
        _viewComponentHelperWrapper.Contextualize(viewContext);

        var result = await _viewComponentHelperWrapper.InvokeAsync(viewComponent.TypeInfo.AsType(), blockListItem);
        result.WriteTo(sw, HtmlEncoder.Default);
        return sw.ToString();
    }

    private sealed class FakeView : IView
    {
        public string Path => string.Empty;

        public Task RenderAsync(ViewContext context)
        {
            return Task.CompletedTask;
        }
    }
}
