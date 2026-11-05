using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.Controllers;

/// <summary>Kategorie i podkategorie ogłoszeń.</summary>
[ApiController]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    /// <summary>Wszystkie kategorie razem z podkategoriami.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(
        [FromServices] IQueryHandler<GetCategories, IReadOnlyList<CategoryDto>> handler,
        CancellationToken cancellationToken)
        => Ok(await handler.HandleAsync(new GetCategories(), cancellationToken));

    [HttpGet("{categoryId:guid}")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetById(
        Guid categoryId,
        [FromServices] IQueryHandler<GetCategoryById, CategoryDto?> handler,
        CancellationToken cancellationToken)
    {
        var category = await handler.HandleAsync(new GetCategoryById { CategoryId = categoryId }, cancellationToken);

        return category is null ? NotFound() : Ok(category);
    }
}
