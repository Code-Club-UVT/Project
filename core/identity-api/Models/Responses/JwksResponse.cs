namespace Identity.Models.Responses;

/// <summary>JSON Web Key Set (RFC 7517). Property names serialise as <c>keys</c>, <c>kty</c>, <c>crv</c>, ...</summary>
public sealed record JwksResponse(IReadOnlyList<JsonWebKeyResponse> Keys);

/// <summary>Public EC key (RFC 7518): P-256 for ES256 signatures.</summary>
public sealed record JsonWebKeyResponse(string Kty, string Crv, string X, string Y, string Use, string Alg, string Kid);
