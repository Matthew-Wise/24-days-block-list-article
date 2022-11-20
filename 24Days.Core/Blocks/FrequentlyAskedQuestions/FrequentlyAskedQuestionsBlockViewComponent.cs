using _Days.Models.Content;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace _24Days.Core.Blocks.FrequentlyAskedQuestions
{
    public class FrequentlyAskedQuestionsBlockViewComponent : ViewComponent
    {
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly IPublishedValueFallback _publishedValueFallback;

        public FrequentlyAskedQuestionsBlockViewComponent(IUmbracoContextAccessor umbracoContextAccessor, IPublishedValueFallback publishedValueFallback)
        {
            _umbracoContextAccessor = umbracoContextAccessor;
            _publishedValueFallback = publishedValueFallback;
        }

        public IViewComponentResult Invoke(BlockListItem<FrequentlyAskedQuestionsBlock> block)
        {
            var model = new FaqBlockViewModel(block.Content, _publishedValueFallback);

            using var umbracoContext = _umbracoContextAccessor.GetRequiredUmbracoContext();
            var dataFolder = umbracoContext?.Content?.GetAtRoot().FirstOrDefault(x => x.IsDocumentType(DataFolder.ModelTypeAlias));


            var faqFolders = dataFolder?.Children<FrequentlyAskedQuestionFolder>();
            if (faqFolders == null)
            {
                return Content(string.Empty);
            }

            foreach (var faqFolder in faqFolders)
            {
                var items = faqFolder.Children<FrequentlyAskedQuestion>();
                if (items != null)
                {
                    model.FaqItems.AddRange(items);
                }
            }

            return model.FaqItems.Any() ? View(model) : Content(string.Empty);
        }
    }
}
