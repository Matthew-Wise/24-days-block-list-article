using _Days.Models.Content;
using J2N.Collections.Generic;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace _24Days.Core.Blocks.FrequentlyAskedQuestions
{
    public class FaqBlockViewModel : PublishedElementWrapped
    {
        public FaqBlockViewModel(FrequentlyAskedQuestionsBlock content, IPublishedValueFallback publishedValueFallback) : base(content, publishedValueFallback)
        {
            Content = content;
        }

        public List<FrequentlyAskedQuestion> FaqItems { get; set; } = new List<FrequentlyAskedQuestion>();

        public FrequentlyAskedQuestionsBlock Content { get; private set; }
    }
}