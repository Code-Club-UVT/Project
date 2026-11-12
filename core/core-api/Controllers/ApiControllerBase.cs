using Microsoft.AspNetCore.Mvc;
using PlatformKit.Web;

namespace CoreApi.Controllers;

/// <summary>Base for the public controllers: routes under <c>api/[controller]</c> (the gateway serves them at <c>/api/core/...</c>).</summary>
[Route("api/[controller]")]
public abstract class ApiControllerBase : ResultControllerBase;
