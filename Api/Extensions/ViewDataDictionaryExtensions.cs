using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Application.Api.Extensions;

/// <summary>
/// This class contains helper methods (called "extension methods") that add functionality to the
/// ViewDataDictionary, which is a collection of data passed from the controller to the view in an ASP.NET Core application.
/// These methods are specifically used to get bootstrap validation classes (like "is-invalid") for form fields in a Razor view.
/// </summary>
public static class ViewDataDictionaryExtensions
{
    /// <summary>
    /// Returns a CSS class (like "is-invalid") for a specific model property, based on its validation state.
    /// A validation class is used to apply different styles to form fields depending on whether the data entered is valid or not.
    ///
    /// <para>This overload accepts an expression to select the property (recommended for strongly-typed views).</para>
    ///
    /// Example usage:
    ///
    /// In a Razor view, you might have a form field like this:
    /// <code>
    /// <input type="text" class="form-control @ViewData.ValidationClass(m => m.Name)" id="Name" name="Name" />
    /// </code>
    ///
    /// This will check if the "Name" field is valid or invalid and return the appropriate class.
    /// This extension method also works with the <see cref="TagHelper"/> `asp-for` attribute in Razor views.
    /// </summary>
    public static string ValidationClass<TModel, TProperty>(
        this ViewDataDictionary<TModel> viewData,
        Expression<Func<TModel, TProperty>> selector
    ) => viewData.ValidationClass(((MemberExpression)selector.Body).Member.Name);

    /// <summary>
    /// Returns a CSS class (like "is-invalid") for a specific property key, based on its validation state.
    ///
    /// <para>This overload accepts a string property name directly.</para>
    ///
    /// See <see cref="ValidationClass{TModel, TProperty}(ViewDataDictionary{TModel}, Expression{Func{TModel, TProperty}})"/>
    /// for the recommended strongly-typed version and usage examples.
    /// </summary>
    public static string ValidationClass<T>(this ViewDataDictionary<T> viewData, string key) =>
        viewData.ModelState.GetValidationState(key) switch
        {
            ModelValidationState.Unvalidated => string.Empty,
            ModelValidationState.Invalid => "is-invalid",
            ModelValidationState.Valid => string.Empty,
            ModelValidationState.Skipped => string.Empty,
            _ => string.Empty,
        };
}
