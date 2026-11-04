using Microsoft.AspNetCore.Mvc;

namespace CoreApi.Controllers;

/// <summary>Base for the public controllers: routes under <c>api/[controller]</c>.</summary>
[Route("api/[controller]")]
public abstract class ApiControllerBase : ResultControllerBase;
