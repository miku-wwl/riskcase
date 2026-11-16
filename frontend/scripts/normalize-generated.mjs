import { readFile, writeFile } from "node:fs/promises";

const path = new URL("../src/generated/riskcase-api.ts", import.meta.url);
const source = (await readFile(path, "utf8")).replace(/\r\n/g, "\n");

// .NET runtime patches differ in whether Identity's login validation response
// includes a ProblemDetails schema in OpenAPI. Keep the generated client stable
// and retain the structured response parser in both cases.
const loginStart = source.indexOf("    protected processLogin(response: Response): Promise<AccessTokenResponse> {");
const refreshStart = source.indexOf("    /**\n     * Refresh token", loginStart);
if (loginStart < 0 || refreshStart < 0) throw new Error("Could not locate the generated login client method.");

let loginMethod = source.slice(loginStart, refreshStart);
if (!loginMethod.includes("HttpValidationProblemDetails.fromJS(resultData400)")) {
  const rawResponseBranch = `        } else if (status === 400) {
            return response.text().then((_responseText) => {
            return throwException("Bad Request", status, _responseText, _headers);
            });
        }`;
  const structuredResponseBranch = `        } else if (status === 400) {
            return response.text().then((_responseText) => {
            let result400: any = null;
            let resultData400 = _responseText === "" ? null : JSON.parse(_responseText, this.jsonParseReviver);
            result400 = HttpValidationProblemDetails.fromJS(resultData400);
            return throwException("Bad Request", status, _responseText, _headers, result400);
            });
        }`;
  if (!loginMethod.includes(rawResponseBranch)) throw new Error("Could not normalize the generated login validation response.");
  loginMethod = loginMethod.replace(rawResponseBranch, structuredResponseBranch);
}

const stableSource = `${source.slice(0, loginStart)}${loginMethod}${source.slice(refreshStart)}`;
const normalized = `${stableSource.replace(/\r\n/g, "\n").replace(/[\t ]+$/gm, "").replace(/\n*$/, "\n")}`;
await writeFile(path, normalized, "utf8");
