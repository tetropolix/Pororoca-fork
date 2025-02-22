using Pororoca.Domain.Features.Entities.Pororoca;
using Pororoca.Domain.Features.Entities.Pororoca.Http;
using Pororoca.Domain.Features.VariableResolution;
using static Pororoca.Domain.Features.Common.AvailablePororocaRequestSelectionOptions;
using static Pororoca.Domain.Features.TranslateRequest.Common.PororocaRequestCommonTranslator;
using static Pororoca.Domain.Features.TranslateRequest.Common.PororocaRequestCommonValidator;

namespace Pororoca.Domain.Features.TranslateRequest.Http;

public static class PororocaHttpRequestValidator
{
    public static bool IsValidRequest(IEnumerable<PororocaVariable> effectiveVars, PororocaRequestAuth? collectionScopedAuth, PororocaHttpRequest req, out string? errorCode) =>
        IsValidRequest(IsHttpVersionAvailableInOS, File.Exists, effectiveVars, collectionScopedAuth, req, out errorCode);

    internal static bool IsValidRequest(HttpVersionAvailableVerifier httpVersionOSVerifier, FileExistsVerifier fileExistsVerifier, IEnumerable<PororocaVariable> effectiveVars, PororocaRequestAuth? collectionScopedAuth, PororocaHttpRequest req, out string? errorCode) =>
        TryResolveRequestUri(effectiveVars, req.Url, out _, out errorCode)
        && httpVersionOSVerifier(req.HttpVersion, out errorCode)
        && HasValidContentTypeForReqBody(req, out errorCode)
        && CheckReqBodyFileExists(effectiveVars, req, fileExistsVerifier, out errorCode)
        && ValidateAuthParams(effectiveVars, fileExistsVerifier, ChooseRequestAuth(collectionScopedAuth, req.CustomAuth), out errorCode);

    private static bool HasValidContentTypeForReqBody(PororocaHttpRequest req, out string? errorCode)
    {
        var bodyMode = req.Body?.Mode;

        if (bodyMode == PororocaHttpRequestBodyMode.Raw)
        {
            bool isBlank = string.IsNullOrWhiteSpace(req.Body?.ContentType);
            bool isInvalid = !IsValidContentType(req.Body!.ContentType);
            errorCode = isBlank ? TranslateRequestErrors.ContentTypeCannotBeBlankReqBodyRaw :
                        isInvalid ? TranslateRequestErrors.InvalidContentTypeRaw :
                        null;
            return errorCode == null;
        }
        else if (bodyMode == PororocaHttpRequestBodyMode.File)
        {
            bool isBlank = string.IsNullOrWhiteSpace(req.Body?.ContentType);
            bool isInvalid = !IsValidContentType(req.Body!.ContentType);
            errorCode = isBlank ? TranslateRequestErrors.ContentTypeCannotBeBlankReqBodyFile :
                        isInvalid ? TranslateRequestErrors.InvalidContentTypeFile :
                        null;
            return errorCode == null;
        }
        else if (bodyMode == PororocaHttpRequestBodyMode.FormData)
        {
            bool anyInvalid = req.Body!.FormDataValues!.Any(fd => fd.Enabled && !IsValidContentType(fd.ContentType));
            errorCode = anyInvalid ? TranslateRequestErrors.InvalidContentTypeFormData : null;
            return errorCode == null;
        }
        else
        {
            errorCode = null;
            return true;
        }
    }

    private static bool CheckReqBodyFileExists(IEnumerable<PororocaVariable> effectiveVars, PororocaHttpRequest req, FileExistsVerifier fileExistsVerifier, out string? errorCode)
    {
        var bodyMode = req.Body?.Mode;

        if (bodyMode == PororocaHttpRequestBodyMode.File)
        {
            string resolvedFilePath = IPororocaVariableResolver.ReplaceTemplates(req.Body!.FileSrcPath!, effectiveVars);
            bool fileFound = fileExistsVerifier.Invoke(resolvedFilePath);
            errorCode = fileFound ? null : TranslateRequestErrors.ReqBodyFileNotFound;
            return errorCode == null;
        }
        else if (bodyMode == PororocaHttpRequestBodyMode.FormData)
        {
            foreach (var fd in req.Body!.FormDataValues!)
            {
                if (fd.Enabled && fd.Type == PororocaHttpRequestFormDataParamType.File)
                {
                    string resolvedFilePath = IPororocaVariableResolver.ReplaceTemplates(fd.FileSrcPath!, effectiveVars);
                    if (!fileExistsVerifier.Invoke(resolvedFilePath))
                    {
                        errorCode = TranslateRequestErrors.ReqBodyFileNotFound;
                        return errorCode == null;
                    }
                }
            }

            errorCode = null;
            return true;
        }
        else
        {
            errorCode = null;
            return true;
        }
    }
}