using Pororoca.Domain.Features.Entities.Pororoca;
using Pororoca.Domain.Features.Entities.Pororoca.Http;
using Pororoca.Domain.Features.TranslateRequest;
using Pororoca.Domain.Features.VariableResolution;
using Xunit;
using static Pororoca.Domain.Features.TranslateRequest.Common.PororocaRequestCommonTranslator;

namespace Pororoca.Domain.Tests.Features.TranslateRequest.Common;

public static class PororocaRequestCommonTranslatorTests
{
    private static IEnumerable<PororocaVariable> GetEffectiveVariables(this PororocaCollection col) =>
        ((IPororocaVariableResolver)col).GetEffectiveVariables();

    #region REQUEST URL

    [Fact]
    public static void Should_make_uri_if_valid_url_is_resolved_from_variable()
    {
        // GIVEN
        PororocaCollection col = new(string.Empty);
        col.Variables.Add(new(true, "BaseUrl", "http://www.pudim.com.br", false));

        // WHEN
        bool valid = TryResolveRequestUri(col.GetEffectiveVariables(), "{{BaseUrl}}", out var uri, out string? errorCode);

        // THEN
        Assert.True(valid);
        Assert.Null(errorCode);
        Assert.NotNull(uri);
        Assert.Equal("http://www.pudim.com.br/", uri!.ToString());
    }

    [Fact]
    public static void Should_make_uri_if_valid_url_but_not_resolved_from_variable()
    {
        // GIVEN
        PororocaCollection col = new(string.Empty);
        col.Variables.Add(new(true, "BaseUrl", "http://www.pudim.com.br", false));

        // WHEN
        bool valid = TryResolveRequestUri(col.GetEffectiveVariables(), "https://www.miniclip.com", out var uri, out string? errorCode);

        // THEN
        Assert.True(valid);
        Assert.Null(errorCode);
        Assert.NotNull(uri);
        Assert.Equal("https://www.miniclip.com/", uri!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{{BaseUrl2}}")]
    [InlineData("{{BaseUrl3}}")]
    [InlineData("fafffafaf")]
    public static void Should_return_error_and_not_make_uri_if_invalid_url_pure_or_from_variable(string unresolvedUrl)
    {
        // GIVEN
        PororocaCollection col = new(string.Empty);
        col.Variables.Add(new(true, "BaseUrl", "http://www.pudim.com.br", false));
        col.Variables.Add(new(false, "BaseUrl2", "https://www.pudim.com.br", false));
        col.Variables.Add(new(true, "BaseUrl3", "https:/www.aaa.gov", false));

        // WHEN
        bool valid = TryResolveRequestUri(col.GetEffectiveVariables(), unresolvedUrl, out var uri, out string? errorCode);

        // THEN
        Assert.False(valid);
        Assert.Equal(TranslateRequestErrors.InvalidUrl, errorCode);
        Assert.Null(uri);
    }

    [Theory]
    [InlineData("ftp://192.168.0.1")]
    [InlineData("smtp://user:port@host:25")]
    public static void Should_return_error_and_not_make_uri_if_url_is_not_http_or_websocket(string unresolvedUrl)
    {
        // GIVEN
        PororocaCollection col = new(string.Empty);

        // WHEN
        bool valid = TryResolveRequestUri(col.GetEffectiveVariables(), unresolvedUrl, out var uri, out string? errorCode);

        // THEN
        Assert.False(valid);
        Assert.Equal(TranslateRequestErrors.InvalidUrl, errorCode);
        Assert.Null(uri);
    }

    #endregion

    #region HTTP VERSION

    [Theory]
    [InlineData(1, 0, 1.0)]
    [InlineData(1, 1, 1.1)]
    [InlineData(2, 0, 2.0)]
    [InlineData(3, 0, 3.0)]
    public static void Should_make_http_version_object_correctly(int versionMajor, int versionMinor, decimal httpVersion)
    {
        // GIVEN

        // WHEN
        var v = ResolveHttpVersion(httpVersion);

        // THEN
        Assert.Equal(versionMajor, v.Major);
        Assert.Equal(versionMinor, v.Minor);
    }

    #endregion

    #region HTTP HEADERS

    [Theory]
    [InlineData("Allow")]
    [InlineData("Content-Disposition")]
    [InlineData("Content-Encoding")]
    [InlineData("Content-Language")]
    [InlineData("Content-Length")]
    [InlineData("Content-Location")]
    [InlineData("Content-MD5")]
    [InlineData("Content-Range")]
    [InlineData("Content-Type")]
    [InlineData("Expires")]
    [InlineData("Last-Modified")]
    public static void Should_detect_content_http_headers_by_the_specification(string headerName) =>
        Assert.True(IsContentHeader(headerName));

    [Theory]
    [InlineData("Date")]
    [InlineData("Authorization")]
    public static void Should_detect_non_content_http_headers_by_the_specification(string headerName) =>
        Assert.False(IsContentHeader(headerName));

    [Fact]
    public static void Should_resolve_content_headers_correctly()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "ExpiresHeader", "Expires", false));
        col.Variables.Add(new(true, "ExpiresAt", "2021-12-02", false));

        var headers = new PororocaKeyValueParam[]
        {
            new(true, "Content-Type", "application/json"),
            new(true, "Content-Language", "pt-BR"),
            new(false, "Content-MD5", "md5"),
            new(false, "{{ExpiresHeader}}", "2020-05-01"),
            new(true, "{{ExpiresHeader}}", "{{ExpiresAt}}"),
            new(true, "{{ExpiresHeader}}", "{{ExpiresAt2}}"),
            new(true, "Date", "2021-12-01")
        };

        // WHEN
        var contentHeaders = ResolveContentHeaders(col.GetEffectiveVariables(), headers);

        // THEN
        Assert.Equal(2, contentHeaders.Count);
        Assert.True(contentHeaders.ContainsKey("Content-Language"));
        Assert.Equal("pt-BR", contentHeaders["Content-Language"]);
        Assert.True(contentHeaders.ContainsKey("Expires"));
        Assert.Equal("2021-12-02", contentHeaders["Expires"]);
    }

    [Fact]
    public static void Should_resolve_non_content_headers_no_auth_correctly()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "CookieHeader", "Cookie", false));
        col.Variables.Add(new(true, "TestCookie2", "cookie2", false));

        var headers = new PororocaKeyValueParam[]
        {
            new(true, "Content-Type", "application/json"),
            new(true, "Content-Language", "pt-BR"),
            new(true, "If-Modified-Since", "2021-10-03"),
            new(false, "{{CookieHeader}}", "{{TestCookie1}}"),
            new(true, "{{CookieHeader}}", "{{TestCookie2}}"),
            new(true, "{{CookieHeader}}", "{{TestCookie3}}")
        };

        // WHEN
        var contentHeaders = ResolveNonContentHeaders(col.GetEffectiveVariables(), col.CollectionScopedAuth, null, headers);

        // THEN
        Assert.Equal(2, contentHeaders.Count);
        Assert.True(contentHeaders.ContainsKey("If-Modified-Since"));
        Assert.Equal("2021-10-03", contentHeaders["If-Modified-Since"]);
        Assert.True(contentHeaders.ContainsKey("Cookie"));
        Assert.Equal("cookie2", contentHeaders["Cookie"]);
    }

    [Fact]
    public static void Should_resolve_non_content_headers_auth_but_not_custom_correctly()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "CookieHeader", "Cookie", false));
        col.Variables.Add(new(true, "TestCookie2", "cookie2", false));
        col.Variables.Add(new(true, "MyAuthHeader", "Authorization", false));
        col.Variables.Add(new(true, "MyAuthToken", "tkn", false));

        var headers = new PororocaKeyValueParam[]
        {
            new(true, "Content-Type", "application/json"),
            new(true, "Content-Language", "pt-BR"),
            new(true, "If-Modified-Since", "2021-10-03"),
            new(false, "{{CookieHeader}}", "{{TestCookie1}}"),
            new(true, "{{CookieHeader}}", "{{TestCookie2}}"),
            new(true, "{{CookieHeader}}", "{{TestCookie3}}"),
            new(true, "{{MyAuthHeader}}", "{{MyAuthToken}}")
        };

        // WHEN
        var contentHeaders = ResolveNonContentHeaders(col.GetEffectiveVariables(), col.CollectionScopedAuth, null, headers);

        // THEN
        Assert.Equal(3, contentHeaders.Count);
        Assert.True(contentHeaders.ContainsKey("If-Modified-Since"));
        Assert.Equal("2021-10-03", contentHeaders["If-Modified-Since"]);
        Assert.True(contentHeaders.ContainsKey("Cookie"));
        Assert.Equal("cookie2", contentHeaders["Cookie"]);
        Assert.True(contentHeaders.ContainsKey("Authorization"));
        Assert.Equal("tkn", contentHeaders["Authorization"]);
    }

    [Fact]
    public static void Should_resolve_non_content_headers_with_custom_basic_auth_correctly()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "CookieHeader", "Cookie", false));
        col.Variables.Add(new(true, "TestCookie2", "cookie2", false));
        col.Variables.Add(new(true, "Username", "usr", false));
        col.Variables.Add(new(true, "Password", "pwd", false));

        var headers = new PororocaKeyValueParam[]
        {
            new(true, "Content-Type", "application/json"),
            new(true, "Content-Language", "pt-BR"),
            new(true, "If-Modified-Since", "2021-10-03"),
            new(false, "{{CookieHeader}}", "{{TestCookie1}}"),
            new(true, "{{CookieHeader}}", "{{TestCookie2}}"),
            new(true, "{{CookieHeader}}", "{{TestCookie3}}")
        };

        var reqAuth = PororocaRequestAuth.MakeBasicAuth("{{Username}}", "{{Password}}");

        // WHEN
        var contentHeaders = ResolveNonContentHeaders(col.GetEffectiveVariables(), col.CollectionScopedAuth, reqAuth, headers);

        // THEN
        Assert.Equal(3, contentHeaders.Count);
        Assert.True(contentHeaders.ContainsKey("If-Modified-Since"));
        Assert.Equal("2021-10-03", contentHeaders["If-Modified-Since"]);
        Assert.True(contentHeaders.ContainsKey("Cookie"));
        Assert.Equal("cookie2", contentHeaders["Cookie"]);
        Assert.True(contentHeaders.ContainsKey("Authorization"));
        Assert.Equal("Basic dXNyOnB3ZA==", contentHeaders["Authorization"]);
    }

    [Fact]
    public static void Should_resolve_non_content_headers_with_custom_bearer_auth_correctly()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "CookieHeader", "Cookie", false));
        col.Variables.Add(new(true, "TestCookie2", "cookie2", false));
        col.Variables.Add(new(true, "BearerToken", "tkn", false));

        var headers = new PororocaKeyValueParam[]
        {
            new(true, "Content-Type", "application/json"),
            new(true, "Content-Language", "pt-BR"),
            new(true, "If-Modified-Since", "2021-10-03"),
            new(false, "{{CookieHeader}}", "{{TestCookie1}}"),
            new(true, "{{CookieHeader}}", "{{TestCookie2}}"),
            new(true, "{{CookieHeader}}", "{{TestCookie3}}")
        };

        var reqAuth = PororocaRequestAuth.MakeBearerAuth("{{BearerToken}}");

        // WHEN
        var contentHeaders = ResolveNonContentHeaders(col.GetEffectiveVariables(), col.CollectionScopedAuth, reqAuth, headers);

        // THEN
        Assert.Equal(3, contentHeaders.Count);
        Assert.True(contentHeaders.ContainsKey("If-Modified-Since"));
        Assert.Equal("2021-10-03", contentHeaders["If-Modified-Since"]);
        Assert.True(contentHeaders.ContainsKey("Cookie"));
        Assert.Equal("cookie2", contentHeaders["Cookie"]);
        Assert.True(contentHeaders.ContainsKey("Authorization"));
        Assert.Equal("Bearer tkn", contentHeaders["Authorization"]);
    }

    [Fact]
    public static void Should_resolve_non_content_headers_with_custom_inherited_from_collection_auth_correctly()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver")
        {
            CollectionScopedAuth = PororocaRequestAuth.MakeBearerAuth("{{BearerToken}}")
        };
        col.Variables.Add(new(true, "CookieHeader", "Cookie", false));
        col.Variables.Add(new(true, "TestCookie2", "cookie2", false));
        col.Variables.Add(new(true, "BearerToken", "tkn", false));

        var headers = new PororocaKeyValueParam[]
        {
            new(true, "Content-Type", "application/json"),
            new(true, "Content-Language", "pt-BR"),
            new(true, "If-Modified-Since", "2021-10-03"),
            new(false, "{{CookieHeader}}", "{{TestCookie1}}"),
            new(true, "{{CookieHeader}}", "{{TestCookie2}}"),
            new(true, "{{CookieHeader}}", "{{TestCookie3}}")
        };

        var reqAuth = PororocaRequestAuth.InheritedFromCollection;

        // WHEN
        var contentHeaders = ResolveNonContentHeaders(col.GetEffectiveVariables(), col.CollectionScopedAuth, reqAuth, headers);

        // THEN
        Assert.Equal(3, contentHeaders.Count);
        Assert.True(contentHeaders.ContainsKey("If-Modified-Since"));
        Assert.Equal("2021-10-03", contentHeaders["If-Modified-Since"]);
        Assert.True(contentHeaders.ContainsKey("Cookie"));
        Assert.Equal("cookie2", contentHeaders["Cookie"]);
        Assert.True(contentHeaders.ContainsKey("Authorization"));
        Assert.Equal("Bearer tkn", contentHeaders["Authorization"]);
    }

    #endregion

    #region AUTH

    [Fact]
    public static void Should_use_no_auth_if_specified()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        // WHEN
        var resolvedAuth = ResolveRequestAuth(col.GetEffectiveVariables(), col.CollectionScopedAuth, reqAuth: null);
        // THEN
        Assert.Null(resolvedAuth);
    }

    [Fact]
    public static void Should_use_request_custom_auth_if_specified()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "CertificateFilePath", "./cert.p12", false));
        col.Variables.Add(new(true, "PrivateKeyFilePassword", "my_pwd", false));

        var reqAuth = PororocaRequestAuth.MakeClientCertificateAuth(PororocaRequestAuthClientCertificateType.Pkcs12, "{{CertificateFilePath}}", null, "{{PrivateKeyFilePassword}}");

        // WHEN
        var resolvedAuth = ResolveRequestAuth(col.GetEffectiveVariables(), col.CollectionScopedAuth, reqAuth);

        // THEN
        Assert.NotNull(resolvedAuth);
        Assert.Equal(PororocaRequestAuthMode.ClientCertificate, resolvedAuth!.Mode);
        Assert.Equal(PororocaRequestAuthClientCertificateType.Pkcs12, resolvedAuth.ClientCertificate!.Type);
        Assert.Equal("./cert.p12", resolvedAuth.ClientCertificate.CertificateFilePath);
        Assert.Null(resolvedAuth.ClientCertificate.PrivateKeyFilePath);
        Assert.Equal("my_pwd", resolvedAuth.ClientCertificate.FilePassword);
    }

    [Fact]
    public static void Should_use_collection_scoped_auth_if_specified()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver")
        {
            CollectionScopedAuth = PororocaRequestAuth.MakeWindowsAuth(false, "{{win_login}}", "{{win_pwd}}", "{{win_domain}}")
        };
        col.Variables.Add(new(true, "win_login", "alexandre123", false));
        col.Variables.Add(new(true, "win_pwd", "my_pwd", false));
        col.Variables.Add(new(true, "win_domain", "alexandre.mydomain.net", false));

        // WHEN
        var resolvedAuth = ResolveRequestAuth(col.GetEffectiveVariables(), col.CollectionScopedAuth, PororocaRequestAuth.InheritedFromCollection);

        // THEN
        Assert.NotNull(resolvedAuth);
        Assert.Equal(PororocaRequestAuthMode.Windows, resolvedAuth!.Mode);
        Assert.Equal("alexandre123", resolvedAuth!.Windows!.Login);
        Assert.Equal("my_pwd", resolvedAuth!.Windows!.Password);
        Assert.Equal("alexandre.mydomain.net", resolvedAuth!.Windows!.Domain);
    }

    #endregion

    #region CLIENT CERTIFICATES

    [Fact]
    public static void Should_resolve_PKCS12_client_certificate_params_correctly()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "CertificateFilePath", "./cert.p12", false));
        col.Variables.Add(new(true, "PrivateKeyFilePassword", "my_pwd", false));

        var reqAuth = PororocaRequestAuth.MakeClientCertificateAuth(PororocaRequestAuthClientCertificateType.Pkcs12, "{{CertificateFilePath}}", null, "{{PrivateKeyFilePassword}}");

        PororocaHttpRequest req = new();
        req.UpdateUrl("http://www.pudim.com.br");
        req.UpdateCustomAuth(reqAuth);

        // WHEN
        var resolvedCert = ResolveClientCertificate(col.GetEffectiveVariables(), reqAuth.ClientCertificate!);

        // THEN
        Assert.NotNull(resolvedCert);
        Assert.Equal(PororocaRequestAuthClientCertificateType.Pkcs12, resolvedCert!.Type);
        Assert.Equal("./cert.p12", resolvedCert.CertificateFilePath);
        Assert.Null(resolvedCert.PrivateKeyFilePath);
        Assert.Equal("my_pwd", resolvedCert.FilePassword);
    }

    [Fact]
    public static void Should_resolve_PEM_client_certificate_params_correctly_separate_private_key()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "CertificateFilePath", "./cert.pem", false));
        col.Variables.Add(new(true, "PrivateKeyFilePath", "./private_key.key", false));
        col.Variables.Add(new(true, "PrivateKeyFilePassword", "my_pwd", false));

        var reqAuth = PororocaRequestAuth.MakeClientCertificateAuth(PororocaRequestAuthClientCertificateType.Pem, "{{CertificateFilePath}}", "{{PrivateKeyFilePath}}", "{{PrivateKeyFilePassword}}");

        PororocaHttpRequest req = new();
        req.UpdateUrl("http://www.pudim.com.br");
        req.UpdateCustomAuth(reqAuth);

        // WHEN
        var resolvedCert = ResolveClientCertificate(col.GetEffectiveVariables(), reqAuth.ClientCertificate!);

        // THEN
        Assert.NotNull(resolvedCert);
        Assert.Equal(PororocaRequestAuthClientCertificateType.Pem, resolvedCert!.Type);
        Assert.Equal("./cert.pem", resolvedCert.CertificateFilePath);
        Assert.Equal("./private_key.key", resolvedCert.PrivateKeyFilePath);
        Assert.Equal("my_pwd", resolvedCert.FilePassword);
    }

    [Fact]
    public static void Should_resolve_PEM_client_certificate_params_correctly_conjoined_private_key()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "CertificateFilePath", "./cert.pem", false));
        col.Variables.Add(new(true, "FilePassword", "my_pwd", false));

        var reqAuth = PororocaRequestAuth.MakeClientCertificateAuth(PororocaRequestAuthClientCertificateType.Pem, "{{CertificateFilePath}}", null, "{{FilePassword}}");

        PororocaHttpRequest req = new();
        req.UpdateUrl("http://www.pudim.com.br");
        req.UpdateCustomAuth(reqAuth);

        // WHEN
        var resolvedCert = ResolveClientCertificate(col.GetEffectiveVariables(), reqAuth.ClientCertificate!);

        // THEN
        Assert.NotNull(resolvedCert);
        Assert.Equal(PororocaRequestAuthClientCertificateType.Pem, resolvedCert!.Type);
        Assert.Equal("./cert.pem", resolvedCert.CertificateFilePath);
        Assert.Null(resolvedCert.PrivateKeyFilePath);
        Assert.Equal("my_pwd", resolvedCert.FilePassword);
    }

    #endregion

    #region WINDOWS AUTHENTICATION

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void Should_resolve_windows_authentication_values(bool useCurrentUser)
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "win_login", "alexandre123", false));
        col.Variables.Add(new(true, "win_pwd", "my_pwd", false));
        col.Variables.Add(new(true, "win_domain", "alexandre.mydomain.net", false));

        var reqAuth = PororocaRequestAuth.MakeWindowsAuth(useCurrentUser, "{{win_login}}", "{{win_pwd}}", "{{win_domain}}");

        PororocaHttpRequest req = new();
        req.UpdateCustomAuth(reqAuth);

        // WHEN
        var resolvedWinAuth = ResolveWindowsAuth(col.GetEffectiveVariables(), reqAuth.Windows!);

        // THEN
        Assert.NotNull(resolvedWinAuth);
        Assert.Equal(useCurrentUser, resolvedWinAuth!.UseCurrentUser);
        if (useCurrentUser == true)
        {
            Assert.Null(resolvedWinAuth.Login);
            Assert.Null(resolvedWinAuth.Password);
            Assert.Null(resolvedWinAuth.Domain);
        }
        else
        {
            Assert.Equal("alexandre123", resolvedWinAuth.Login);
            Assert.Equal("my_pwd", resolvedWinAuth.Password);
            Assert.Equal("alexandre.mydomain.net", resolvedWinAuth.Domain);
        }
    }

    #endregion
}