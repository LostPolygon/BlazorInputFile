using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BlazorInputFile {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    internal class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter {
        public void OnResourceExecuting(ResourceExecutingContext context) {
            IList<IValueProviderFactory> factories = context.ValueProviderFactories;
            factories.RemoveType<FormValueProviderFactory>();
            factories.RemoveType<FormFileValueProviderFactory>();
            factories.RemoveType<JQueryFormValueProviderFactory>();
        }

        public void OnResourceExecuted(ResourceExecutedContext context) {
        }
    }
}
