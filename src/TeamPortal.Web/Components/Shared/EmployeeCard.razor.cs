using Microsoft.AspNetCore.Components;
using TeamPortal.Application.Dtos;

namespace TeamPortal.Web.Components.Shared;

public partial class EmployeeCard : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public EmployeeCardDto Employee { get; set; } = null!;

    /// <summary>
    /// Opens the Add/Edit Employee modal for this person. The pencil is only
    /// rendered when a handler is bound, so no inert buttons appear.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnEdit { get; set; }
}
