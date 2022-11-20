namespace Morgan.Test.Feature.Backoffice;

using System.Reflection;
using _24Days.Core.Helpers;
using _24Days.Core.Services;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Extensions;
using Umbraco.Cms.Tests.Integration.Implementations;
using _24Days.Core.Blocks.FrequentlyAskedQuestions;

public class BackOfficePreviewServiceTests
{
	private Mock<IViewComponentHelperWrapper> _viewComponentHelper = null!;

	private BackOfficePreviewService _service = null!;

	private object _blockListItem;

	private Type _foundVcType;

	private Mock<IPublishedContentCache> _contentCache = null!;
	private Mock<IRazorViewEngine> _razorViewEngine = null!;

	[SetUp]
	public void Setup()
	{
		var testHelper = new TestHelper();

        _viewComponentHelper = new Mock<IViewComponentHelperWrapper>();
		_viewComponentHelper.Setup(h => h.InvokeAsync(It.IsAny<Type>(), It.IsAny<object>()))
			.Callback<Type, object>((n, b) =>
			{
				_foundVcType = n;
				_blockListItem = b;
				}).Returns<Type, object>((_, _) => Task.FromResult(Mock.Of<IHtmlContent>()));
        
        _contentCache = new Mock<IPublishedContentCache>();
		_razorViewEngine = new Mock<IRazorViewEngine>();
		_service = new BackOfficePreviewService(
			new BlockEditorConverter(GetPublishedSnapshotAccessor(), new NoopPublishedModelFactory()),
			Mock.Of<ITempDataProvider>(),
            testHelper.GetTypeFinder(),
			Mock.Of<IPublishedValueFallback>(),
			_viewComponentHelper.Object,
			GetViewComponentSelector(),
			_razorViewEngine.Object);
	}

	[Test]
	public async Task CallPreview_WithValidViewComponentBlockType()
	{
		var imageKey = Guid.NewGuid();
		AddToContentCache(imageKey, FrequentlyAskedQuestionsBlock.ModelTypeAlias);
		AddToContentCache(Guid.NewGuid(), "invalid");
		var blockData = Mock.Of<BlockItemData>(
			data => data.Udi == Udi.Create(Constants.UdiEntityType.Element, imageKey)
			        && data.ContentTypeAlias == FrequentlyAskedQuestionsBlock.ModelTypeAlias && data.ContentTypeKey == imageKey);
		var _ = await _service.GetMarkupForBlock(
			blockData,
			new ControllerContext(
				new ActionContext(new DefaultHttpContext(), new RouteData(), Mock.Of<ControllerActionDescriptor>())));

		_viewComponentHelper.Verify(h => h.Contextualize(It.IsAny<ViewContext>()), Times.Once);

		_foundVcType.Name.Should().Be(typeof(FrequentlyAskedQuestionsBlockViewComponent).Name);
        _blockListItem?.Should().BeOfType<BlockListItem<FrequentlyAskedQuestionsBlock>>();
	}


	[Test]
	public async Task CallPreview_WithPartialBlock()
	{
		var key = Guid.NewGuid();
		AddToContentCache(key, TextAndImageBlock.ModelTypeAlias);
		var blockData = Mock.Of<BlockItemData>(
			data => data.Udi == Udi.Create(Constants.UdiEntityType.Element, Guid.NewGuid())
			        && data.ContentTypeAlias == TextAndImageBlock.ModelTypeAlias &&
			        data.ContentTypeKey == key);

		var _ = await _service.GetMarkupForBlock(
			blockData,
			new ControllerContext(
				new ActionContext(new DefaultHttpContext(), new RouteData(), Mock.Of<ControllerActionDescriptor>())));


		_viewComponentHelper.Verify(h => h.Contextualize(It.IsAny<ViewContext>()), Times.Never);
		_razorViewEngine.Verify(r => r.GetView(It.IsAny<string>(), It.IsAny<string>(), false), Times.Once);
	}


	[Test]
	public async Task Throws_WithInvalidElement()
	{
		AddToContentCache(Guid.NewGuid(), FrequentlyAskedQuestionsBlock.ModelTypeAlias);
		var blockData = Mock.Of<BlockItemData>(
			data => data.Udi == Udi.Create(Constants.UdiEntityType.Element, Guid.NewGuid())
			        && data.ContentTypeAlias == FrequentlyAskedQuestionsBlock.ModelTypeAlias && data.ContentTypeKey == Guid.NewGuid());

		Func<Task> act = () => _service.GetMarkupForBlock(
			blockData,
			new ControllerContext(
				new ActionContext(new DefaultHttpContext(), new RouteData(), Mock.Of<ControllerActionDescriptor>())));

		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*Element {FrequentlyAskedQuestionsBlock.ModelTypeAlias}");
		_viewComponentHelper.Verify(h => h.Contextualize(It.IsAny<ViewContext>()), Times.Never);
	}

	[Test]
	public async Task Throws_WithInvalidBlock()
	{
		var key = Guid.NewGuid();
		AddToContentCache(key, "invalid");
		var blockData = Mock.Of<BlockItemData>(
			data => data.Udi == Udi.Create(Constants.UdiEntityType.Element, key)
			        && data.ContentTypeAlias == "invalid" && data.ContentTypeKey == key);

		Func<Task> act = () => _service.GetMarkupForBlock(
			blockData,
			new ControllerContext(
				new ActionContext(new DefaultHttpContext(), new RouteData(), Mock.Of<ControllerActionDescriptor>())));

		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*BlockType Invalid");
		_viewComponentHelper.Verify(h => h.Contextualize(It.IsAny<ViewContext>()), Times.Never);
	}


	private void AddToContentCache(Guid key, string alias)
	{
		var contentType = Mock.Of<IPublishedContentType>(
			x => x.IsElement == true && x.Key == key && x.Alias == alias);
		_contentCache.Setup(x => x.GetContentType(key)).Returns(contentType);
	}

	private IPublishedSnapshotAccessor GetPublishedSnapshotAccessor()
	{
		var publishedSnapshot = Mock.Of<IPublishedSnapshot>(x => x.Content == _contentCache.Object);
		var publishedSnapshotAccessor = new Mock<IPublishedSnapshotAccessor>();
		publishedSnapshotAccessor.Setup(x => x.TryGetPublishedSnapshot(out publishedSnapshot)).Returns(true);
		return publishedSnapshotAccessor.Object;
	}

	private static IViewComponentSelector GetViewComponentSelector()
	{
		var viewComponents = typeof(FrequentlyAskedQuestionsBlockViewComponent).Assembly
			.GetTypes()
			.Where(x => x.GetTypeInfo().BaseType == typeof(ViewComponent)).Select(vc =>
			{
				var methodInfo = vc.GetMethod("Invoke")!;
				return new ViewComponentDescriptor
				{
					FullName = vc.FullName,
					ShortName = vc.Name.TrimEnd(nameof(ViewComponent)),
					TypeInfo = vc.GetTypeInfo(),
					MethodInfo = methodInfo,
					Parameters = methodInfo.GetParameters()
				};
			}).ToList();
		var vcCollection = new ViewComponentDescriptorCollection(viewComponents, 0);

		return new DefaultViewComponentSelector(
			Mock.Of<IViewComponentDescriptorCollectionProvider>(p => p.ViewComponents == vcCollection)
		);
	}
}
