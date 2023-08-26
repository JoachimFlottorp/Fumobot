﻿using System.Text.Json.Serialization;

namespace Fumo.ThirdParty.ThreeLetterAPI.Response;

public record UserByIDResponse(
    [property: JsonPropertyName("user")] InnerUser User
);

public record InnerUser(
    [property: JsonPropertyName("id")] string ID,
    [property: JsonPropertyName("login")] string Login
);

