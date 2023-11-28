using Pororoca.Domain.Features.Entities.Pororoca;
using Pororoca.Domain.Features.Entities.Pororoca.Http;
using Pororoca.Domain.Features.TranslateRequest;
using Pororoca.Domain.Features.VariableResolution;
using Xunit;
using static Pororoca.Domain.Features.TranslateRequest.Http.PororocaHttpRequestTranslator;

namespace Pororoca.Domain.Tests.Features.TranslateRequest.Http;

public static class PororocaHttpRequestTranslatorTests
{
    private static IEnumerable<PororocaVariable> GetEffectiveVariables(this PororocaCollection col) =>
        ((IPororocaVariableResolver)col).GetEffectiveVariables();

    #region ESS TESTS

    [Fact]
    public static void TryTranslateRequestWithBadEffectiveVars()
    {
        // GIVEN
        var varResolver = MockVariableResolver("myID", "3162");
        PororocaHttpRequest req = new();
        HttpRequestMessage? reqMsg;
        string? errorCode;

        bool result = TryTranslateRequest(varResolver.GetEffectiveVariables(), null, req,out reqMsg, out errorCode);

        Assert.False(result);
    }

    [Fact]
    public static void SetTryTranslateRequestErrorCode()
    {
        // GIVEN
        var varResolver = MockVariableResolver("myID", "3162");
        PororocaHttpRequest req = new PororocaHttpRequest();
        req.UpdateUrl("https://localhost:8000");
        req.UpdateMethod(null!);
        HttpRequestMessage? reqMsg;
        string? errorCode;

        bool result = TryTranslateRequest(varResolver.GetEffectiveVariables(), null, req ,out reqMsg, out errorCode);

        Assert.False(result);
        Assert.NotNull(errorCode);
        Assert.Equal(TranslateRequestErrors.UnknownRequestTranslationError,errorCode);
    }


    #endregion

    #region MOCKERS

    private static IPororocaVariableResolver MockVariableResolver(string key, string value)
    {
        PororocaCollection col = new("col");
        col.Variables.Add(new(true, key, value, false));
        return col;
    }

    #endregion

    #region HTTP BODY

    [Fact]
    public static void Should_resolve_form_url_encoded_key_values_correctly()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "keyX", "key3", false));
        col.Variables.Add(new(true, "keyXvalue", "value3", false));

        var formUrlEncodedParams = new PororocaKeyValueParam[]
        {
            new(true, "key1", "abc"),
            new(true, "key1", "def"),
            new(false, "key2", "ghi"),
            new(true, "{{keyX}}", "{{keyXvalue}}")
        };

        PororocaHttpRequest req = new();
        PororocaHttpRequestBody body = new();
        body.SetUrlEncodedContent(formUrlEncodedParams);
        req.UpdateBody(body);

        // WHEN
        var resolvedUrlEncoded = ResolveFormUrlEncodedKeyValues(col.GetEffectiveVariables(), req.Body!);

        // THEN
        Assert.Equal(2, resolvedUrlEncoded.Count);
        Assert.True(resolvedUrlEncoded.ContainsKey("key1"));
        Assert.Equal("abc", resolvedUrlEncoded["key1"]);
        Assert.True(resolvedUrlEncoded.ContainsKey("key3"));
        Assert.Equal("value3", resolvedUrlEncoded["key3"]);
    }

    [Fact]
    public static void Should_resolve_no_content_correctly()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        Dictionary<string, string> resolvedContentHeaders = new();

        PororocaHttpRequest req = new();

        // WHEN
        var resolvedReqContent = ResolveRequestContent(col.GetEffectiveVariables(), req.Body, resolvedContentHeaders);

        // THEN
        Assert.Null(resolvedReqContent);
    }

    [Fact]
    public static async Task Should_resolve_raw_content_correctly()
    {
        // GIVEN
        var varResolver = MockVariableResolver("myID", "3162");
        Dictionary<string, string> resolvedContentHeaders = new(1)
        {
            { "Content-Language", "pt-BR" }
        };

        PororocaHttpRequest req = new();
        PororocaHttpRequestBody body = new();
        body.SetRawContent("{\"id\":{{myID}}}", "application/json");
        req.UpdateBody(body);

        // WHEN
        var resolvedReqContent = ResolveRequestContent(varResolver.GetEffectiveVariables(), req.Body, resolvedContentHeaders);

        // THEN
        Assert.NotNull(resolvedReqContent);
        Assert.True(resolvedReqContent is StringContent);
        Assert.NotNull(resolvedReqContent!.Headers.ContentType);
        Assert.Equal("application/json", resolvedReqContent.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", resolvedReqContent.Headers.ContentType!.CharSet);
        Assert.Contains("pt-BR", resolvedReqContent!.Headers.ContentLanguage);

        string? contentText = await resolvedReqContent.ReadAsStringAsync();
        Assert.Equal("{\"id\":3162}", contentText);
    }

    [Fact]
    public static async Task Should_resolve_file_content_correctly()
    {
        // GIVEN
        string testFilePath = GetTestFilePath("testfilecontent1.json");
        var varResolver = MockVariableResolver("MyFilePath", testFilePath);
        Dictionary<string, string> resolvedContentHeaders = new(1)
        {
            { "Content-Language", "pt-BR" }
        };

        PororocaHttpRequest req = new();
        PororocaHttpRequestBody body = new();
        body.SetFileContent("{{MyFilePath}}", "application/json");
        req.UpdateBody(body);

        // WHEN
        var resolvedReqContent = ResolveRequestContent(varResolver.GetEffectiveVariables(), req.Body, resolvedContentHeaders);

        // THEN
        Assert.NotNull(resolvedReqContent);
        Assert.True(resolvedReqContent is StreamContent);
        Assert.NotNull(resolvedReqContent!.Headers.ContentType);
        Assert.Equal("application/json", resolvedReqContent.Headers.ContentType!.MediaType);
        Assert.Contains("pt-BR", resolvedReqContent!.Headers.ContentLanguage);

        string? contentText = await resolvedReqContent.ReadAsStringAsync();
        Assert.Equal("{\"id\":1}", contentText);
    }

    [Fact]
    public static async Task Should_resolve_graphql_content_correctly()
    {
        // GIVEN
        const string qry = "myGraphQlQuery";
        const string variables = "{\"id\":{{CocoId}}}";
        var varResolver = MockVariableResolver("CocoId", "19");
        Dictionary<string, string> resolvedContentHeaders = new(1)
        {
            { "Content-Language", "pt-BR" }
        };

        PororocaHttpRequest req = new();
        PororocaHttpRequestBody body = new();
        body.SetGraphQlContent(qry, variables);
        req.UpdateBody(body);

        // WHEN
        var resolvedReqContent = ResolveRequestContent(varResolver.GetEffectiveVariables(), req.Body, resolvedContentHeaders);

        // THEN
        Assert.NotNull(resolvedReqContent);
        Assert.True(resolvedReqContent is StringContent);
        Assert.NotNull(resolvedReqContent!.Headers.ContentType);
        Assert.Equal("application/json", resolvedReqContent.Headers.ContentType!.MediaType);
        Assert.Contains("pt-BR", resolvedReqContent!.Headers.ContentLanguage);

        string? contentText = await resolvedReqContent.ReadAsStringAsync();
        Assert.Equal("{\"query\":\"myGraphQlQuery\",\"variables\":{\"id\":19}}", contentText);
    }

    [Fact]
    public static async Task Should_resolve_form_url_encoded_content_correctly()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "keyX", "key3", false));
        col.Variables.Add(new(true, "keyXvalue", "value3", false));
        Dictionary<string, string> resolvedContentHeaders = new(1)
        {
            { "Content-Language", "pt-BR" }
        };

        var formUrlEncodedParams = new PororocaKeyValueParam[]
        {
            new(true, "key1", "abc"),
            new(true, "key1", "def"),
            new(false, "key2", "ghi"),
            new(true, "{{keyX}}", "{{keyXvalue}}")
        };

        PororocaHttpRequest req = new();
        PororocaHttpRequestBody body = new();
        body.SetUrlEncodedContent(formUrlEncodedParams);
        req.UpdateBody(body);

        // WHEN
        var resolvedReqContent = ResolveRequestContent(col.GetEffectiveVariables(), req.Body, resolvedContentHeaders);

        // THEN
        Assert.NotNull(resolvedReqContent);
        Assert.NotNull(resolvedReqContent!.Headers.ContentType);
        Assert.Equal("application/x-www-form-urlencoded", resolvedReqContent.Headers.ContentType!.MediaType);
        Assert.Contains("pt-BR", resolvedReqContent!.Headers.ContentLanguage);

        string? contentText = await resolvedReqContent.ReadAsStringAsync();
        Assert.Equal("key1=abc&key3=value3", contentText);
    }


    [Fact]
    public static async Task Should_resolve_form_data_content_correctly()
    {
        // GIVEN
        PororocaCollection col = new("VarResolver");
        col.Variables.Add(new(true, "keyX", "key3", false));
        col.Variables.Add(new(true, "keyXvalue", "value3", false));
        string fileName = "testfilecontent2.json";
        col.Variables.Add(new(true, "MyFileName", fileName, false));
        Dictionary<string, string> resolvedContentHeaders = new(1)
        {
            { "Content-Language", "pt-BR" }
        };

        var p1 = PororocaHttpRequestFormDataParam.MakeTextParam(true, "key1", "oi", "text/plain");
        var p2 = PororocaHttpRequestFormDataParam.MakeTextParam(true, "key1", "oi2", "text/plain");
        var p3 = PororocaHttpRequestFormDataParam.MakeTextParam(false, "key2", "oi2", "text/plain");
        var p4 = PororocaHttpRequestFormDataParam.MakeTextParam(true, "{{keyX}}", "{{keyXvalue}}", "application/json");
        string testFilePath = GetTestFilePath(fileName);
        var p5 = PororocaHttpRequestFormDataParam.MakeFileParam(true, "key4", testFilePath.Replace("testfilecontent2.json", "{{MyFileName}}"), "application/json");

        var formDataParams = new[] { p1, p2, p3, p4, p5 };

        PororocaHttpRequest req = new();
        PororocaHttpRequestBody body = new();
        body.SetFormDataContent(formDataParams);
        req.UpdateBody(body);

        // WHEN
        var resolvedReqContent = ResolveRequestContent(col.GetEffectiveVariables(), req.Body, resolvedContentHeaders);

        // THEN
        Assert.NotNull(resolvedReqContent);
        Assert.NotNull(resolvedReqContent!.Headers.ContentType);
        Assert.Equal("multipart/form-data", resolvedReqContent.Headers.ContentType!.MediaType);
        Assert.Contains("pt-BR", resolvedReqContent!.Headers.ContentLanguage);

        Assert.True(resolvedReqContent is MultipartFormDataContent);
        var castedContent = (MultipartFormDataContent)resolvedReqContent;
        Assert.Equal(4, castedContent.Count());

        var p1Content = (StringContent)castedContent.ElementAt(0);
        string? p1ContentText = await p1Content.ReadAsStringAsync();
        Assert.Equal("oi", p1ContentText);
        Assert.NotNull(p1Content.Headers.ContentDisposition);
        Assert.Equal("form-data", p1Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("key1", p1Content.Headers.ContentDisposition!.Name);
        Assert.NotNull(p1Content.Headers.ContentType);
        Assert.Equal("text/plain", p1Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", p1Content.Headers.ContentType!.CharSet);

        var p2Content = (StringContent)castedContent.ElementAt(1);
        string? p2ContentText = await p2Content.ReadAsStringAsync();
        Assert.Equal("oi2", p2ContentText);
        Assert.NotNull(p2Content.Headers.ContentDisposition);
        Assert.Equal("form-data", p2Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("key1", p2Content.Headers.ContentDisposition!.Name);
        Assert.NotNull(p2Content.Headers.ContentType);
        Assert.Equal("text/plain", p2Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", p2Content.Headers.ContentType!.CharSet);

        var p3Content = (StringContent)castedContent.ElementAt(2);
        string? p3ContentText = await p3Content.ReadAsStringAsync();
        Assert.Equal("value3", p3ContentText);
        Assert.NotNull(p3Content.Headers.ContentDisposition);
        Assert.Equal("form-data", p3Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("key3", p3Content.Headers.ContentDisposition!.Name);
        Assert.NotNull(p3Content.Headers.ContentType);
        Assert.Equal("application/json", p3Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", p3Content.Headers.ContentType!.CharSet);

        var p4Content = (StreamContent)castedContent.ElementAt(3);
        string? p4ContentText = await p4Content.ReadAsStringAsync();
        Assert.Equal("{\"id\":2}", p4ContentText);
        Assert.NotNull(p4Content.Headers.ContentDisposition);
        Assert.Equal("form-data", p4Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("key4", p4Content.Headers.ContentDisposition!.Name);
        Assert.NotNull(p4Content.Headers.ContentType);
        Assert.Equal("application/json", p4Content.Headers.ContentType!.MediaType);
    }

    #endregion

    private static string GetTestFilePath(string fileName)
    {
        var testDataDirInfo = new DirectoryInfo(Environment.CurrentDirectory).Parent!.Parent!.Parent!;
        return Path.Combine(testDataDirInfo.FullName, "TestData", fileName);
    }
}