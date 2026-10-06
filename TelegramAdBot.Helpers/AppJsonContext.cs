using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TelegramAdBot.Models;

namespace TelegramAdBot.Helpers;

[JsonSerializable(typeof(SavedPositions))]
[JsonSerializable(typeof(KeyAuthInitRequest))]
[JsonSerializable(typeof(KeyAuthLoginRequest))]
[JsonSerializable(typeof(KeyAuthResponse))]
[JsonSerializable(typeof(FirebaseUpdateInfo))]
[JsonSerializable(typeof(CdpMessage))]
[JsonSerializable(typeof(CdpResponse))]
[JsonSerializable(typeof(CdpError))]
[JsonSerializable(typeof(ChromeTargetInfo))]
[JsonSerializable(typeof(ChromeTargetInfo[]))]
[JsonSerializable(typeof(ChromeVersion))]
[JsonSerializable(typeof(ChromeVersionResponse))]
[JsonSerializable(typeof(PickPositionResult))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonDocument))]
[GeneratedCode("System.Text.Json.SourceGeneration", "8.0.14.26413")]
public class AppJsonContext : JsonSerializerContext, IJsonTypeInfoResolver
{
	private JsonTypeInfo<bool>? _Boolean;

	private JsonTypeInfo<Dictionary<string, object>>? _DictionaryStringObject;

	private JsonTypeInfo<JsonDocument>? _JsonDocument;

	private JsonTypeInfo<JsonElement>? _JsonElement;

	private JsonTypeInfo<JsonElement?>? _NullableJsonElement;

	private JsonTypeInfo<CdpError>? _CdpError;

	private JsonTypeInfo<CdpMessage>? _CdpMessage;

	private JsonTypeInfo<CdpResponse>? _CdpResponse;

	private JsonTypeInfo<ChromeTargetInfo>? _ChromeTargetInfo;

	private JsonTypeInfo<ChromeTargetInfo[]>? _ChromeTargetInfoArray;

	private JsonTypeInfo<ChromeVersion>? _ChromeVersion;

	private JsonTypeInfo<ChromeVersionResponse>? _ChromeVersionResponse;

	private JsonTypeInfo<FirebaseUpdateInfo>? _FirebaseUpdateInfo;

	private JsonTypeInfo<KeyAuthInitRequest>? _KeyAuthInitRequest;

	private JsonTypeInfo<KeyAuthLoginRequest>? _KeyAuthLoginRequest;

	private JsonTypeInfo<KeyAuthResponse>? _KeyAuthResponse;

	private JsonTypeInfo<PickPositionResult>? _PickPositionResult;

	private JsonTypeInfo<SavedPositions>? _SavedPositions;

	private JsonTypeInfo<int>? _Int32;

	private JsonTypeInfo<object>? _Object;

	private JsonTypeInfo<string>? _String;

	private static readonly JsonSerializerOptions s_defaultOptions = new JsonSerializerOptions();

	private static readonly JsonEncodedText PropName_code = JsonEncodedText.Encode("code");

	private static readonly JsonEncodedText PropName_message = JsonEncodedText.Encode("message");

	private static readonly JsonEncodedText PropName_id = JsonEncodedText.Encode("id");

	private static readonly JsonEncodedText PropName_method = JsonEncodedText.Encode("method");

	private static readonly JsonEncodedText PropName_params = JsonEncodedText.Encode("params");

	private static readonly JsonEncodedText PropName_sessionId = JsonEncodedText.Encode("sessionId");

	private static readonly JsonEncodedText PropName_result = JsonEncodedText.Encode("result");

	private static readonly JsonEncodedText PropName_error = JsonEncodedText.Encode("error");

	private static readonly JsonEncodedText PropName_type = JsonEncodedText.Encode("type");

	private static readonly JsonEncodedText PropName_title = JsonEncodedText.Encode("title");

	private static readonly JsonEncodedText PropName_url = JsonEncodedText.Encode("url");

	private static readonly JsonEncodedText PropName_webSocketDebuggerUrl = JsonEncodedText.Encode("webSocketDebuggerUrl");

	private static readonly JsonEncodedText PropName_Browser = JsonEncodedText.Encode("Browser");

	private static readonly JsonEncodedText EncodedPropName_50726F746F636F6C2D56657273696F6E = JsonEncodedText.Encode("Protocol-Version");

	private static readonly JsonEncodedText EncodedPropName_557365722D4167656E74 = JsonEncodedText.Encode("User-Agent");

	private static readonly JsonEncodedText EncodedPropName_56382D56657273696F6E = JsonEncodedText.Encode("V8-Version");

	private static readonly JsonEncodedText EncodedPropName_5765624B69742D56657273696F6E = JsonEncodedText.Encode("WebKit-Version");

	private static readonly JsonEncodedText PropName_version = JsonEncodedText.Encode("version");

	private static readonly JsonEncodedText PropName_downloadUrl = JsonEncodedText.Encode("downloadUrl");

	private static readonly JsonEncodedText PropName_notes = JsonEncodedText.Encode("notes");

	private static readonly JsonEncodedText PropName_ver = JsonEncodedText.Encode("ver");

	private static readonly JsonEncodedText PropName_hash = JsonEncodedText.Encode("hash");

	private static readonly JsonEncodedText PropName_enckey = JsonEncodedText.Encode("enckey");

	private static readonly JsonEncodedText PropName_name = JsonEncodedText.Encode("name");

	private static readonly JsonEncodedText PropName_ownerid = JsonEncodedText.Encode("ownerid");

	private static readonly JsonEncodedText PropName_username = JsonEncodedText.Encode("username");

	private static readonly JsonEncodedText PropName_pass = JsonEncodedText.Encode("pass");

	private static readonly JsonEncodedText PropName_hwid = JsonEncodedText.Encode("hwid");

	private static readonly JsonEncodedText PropName_sessionid = JsonEncodedText.Encode("sessionid");

	private static readonly JsonEncodedText PropName_success = JsonEncodedText.Encode("success");

	private static readonly JsonEncodedText PropName_nonce = JsonEncodedText.Encode("nonce");

	private static readonly JsonEncodedText PropName_x = JsonEncodedText.Encode("x");

	private static readonly JsonEncodedText PropName_y = JsonEncodedText.Encode("y");

	private static readonly JsonEncodedText PropName_TextboxX = JsonEncodedText.Encode("TextboxX");

	private static readonly JsonEncodedText PropName_TextboxY = JsonEncodedText.Encode("TextboxY");

	private static readonly JsonEncodedText PropName_EmojiButtonX = JsonEncodedText.Encode("EmojiButtonX");

	private static readonly JsonEncodedText PropName_EmojiButtonY = JsonEncodedText.Encode("EmojiButtonY");

	private static readonly JsonEncodedText PropName_StickerTabX = JsonEncodedText.Encode("StickerTabX");

	private static readonly JsonEncodedText PropName_StickerTabY = JsonEncodedText.Encode("StickerTabY");

	private static readonly JsonEncodedText PropName_StickerX = JsonEncodedText.Encode("StickerX");

	private static readonly JsonEncodedText PropName_StickerY = JsonEncodedText.Encode("StickerY");

	public JsonTypeInfo<bool> Boolean => _Boolean ?? (_Boolean = (JsonTypeInfo<bool>)base.Options.GetTypeInfo(typeof(bool)));

	public JsonTypeInfo<Dictionary<string, object>> DictionaryStringObject => _DictionaryStringObject ?? (_DictionaryStringObject = (JsonTypeInfo<Dictionary<string, object>>)base.Options.GetTypeInfo(typeof(Dictionary<string, object>)));

	public JsonTypeInfo<JsonDocument> JsonDocument => _JsonDocument ?? (_JsonDocument = (JsonTypeInfo<JsonDocument>)base.Options.GetTypeInfo(typeof(JsonDocument)));

	public JsonTypeInfo<JsonElement> JsonElement => _JsonElement ?? (_JsonElement = (JsonTypeInfo<JsonElement>)base.Options.GetTypeInfo(typeof(JsonElement)));

	public JsonTypeInfo<JsonElement?> NullableJsonElement => _NullableJsonElement ?? (_NullableJsonElement = (JsonTypeInfo<JsonElement?>)base.Options.GetTypeInfo(typeof(JsonElement?)));

	public JsonTypeInfo<CdpError> CdpError => _CdpError ?? (_CdpError = (JsonTypeInfo<CdpError>)base.Options.GetTypeInfo(typeof(CdpError)));

	public JsonTypeInfo<CdpMessage> CdpMessage => _CdpMessage ?? (_CdpMessage = (JsonTypeInfo<CdpMessage>)base.Options.GetTypeInfo(typeof(CdpMessage)));

	public JsonTypeInfo<CdpResponse> CdpResponse => _CdpResponse ?? (_CdpResponse = (JsonTypeInfo<CdpResponse>)base.Options.GetTypeInfo(typeof(CdpResponse)));

	public JsonTypeInfo<ChromeTargetInfo> ChromeTargetInfo => _ChromeTargetInfo ?? (_ChromeTargetInfo = (JsonTypeInfo<ChromeTargetInfo>)base.Options.GetTypeInfo(typeof(ChromeTargetInfo)));

	public JsonTypeInfo<ChromeTargetInfo[]> ChromeTargetInfoArray => _ChromeTargetInfoArray ?? (_ChromeTargetInfoArray = (JsonTypeInfo<ChromeTargetInfo[]>)base.Options.GetTypeInfo(typeof(ChromeTargetInfo[])));

	public JsonTypeInfo<ChromeVersion> ChromeVersion => _ChromeVersion ?? (_ChromeVersion = (JsonTypeInfo<ChromeVersion>)base.Options.GetTypeInfo(typeof(ChromeVersion)));

	public JsonTypeInfo<ChromeVersionResponse> ChromeVersionResponse => _ChromeVersionResponse ?? (_ChromeVersionResponse = (JsonTypeInfo<ChromeVersionResponse>)base.Options.GetTypeInfo(typeof(ChromeVersionResponse)));

	public JsonTypeInfo<FirebaseUpdateInfo> FirebaseUpdateInfo => _FirebaseUpdateInfo ?? (_FirebaseUpdateInfo = (JsonTypeInfo<FirebaseUpdateInfo>)base.Options.GetTypeInfo(typeof(FirebaseUpdateInfo)));

	public JsonTypeInfo<KeyAuthInitRequest> KeyAuthInitRequest => _KeyAuthInitRequest ?? (_KeyAuthInitRequest = (JsonTypeInfo<KeyAuthInitRequest>)base.Options.GetTypeInfo(typeof(KeyAuthInitRequest)));

	public JsonTypeInfo<KeyAuthLoginRequest> KeyAuthLoginRequest => _KeyAuthLoginRequest ?? (_KeyAuthLoginRequest = (JsonTypeInfo<KeyAuthLoginRequest>)base.Options.GetTypeInfo(typeof(KeyAuthLoginRequest)));

	public JsonTypeInfo<KeyAuthResponse> KeyAuthResponse => _KeyAuthResponse ?? (_KeyAuthResponse = (JsonTypeInfo<KeyAuthResponse>)base.Options.GetTypeInfo(typeof(KeyAuthResponse)));

	public JsonTypeInfo<PickPositionResult> PickPositionResult => _PickPositionResult ?? (_PickPositionResult = (JsonTypeInfo<PickPositionResult>)base.Options.GetTypeInfo(typeof(PickPositionResult)));

	public JsonTypeInfo<SavedPositions> SavedPositions => _SavedPositions ?? (_SavedPositions = (JsonTypeInfo<SavedPositions>)base.Options.GetTypeInfo(typeof(SavedPositions)));

	public JsonTypeInfo<int> Int32 => _Int32 ?? (_Int32 = (JsonTypeInfo<int>)base.Options.GetTypeInfo(typeof(int)));

	public JsonTypeInfo<object> Object => _Object ?? (_Object = (JsonTypeInfo<object>)base.Options.GetTypeInfo(typeof(object)));

	public JsonTypeInfo<string> String => _String ?? (_String = (JsonTypeInfo<string>)base.Options.GetTypeInfo(typeof(string)));

	public static AppJsonContext Default { get; } = new AppJsonContext(new JsonSerializerOptions(s_defaultOptions));

	protected override JsonSerializerOptions? GeneratedSerializerOptions { get; } = s_defaultOptions;

	private JsonTypeInfo<bool> Create_Boolean(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<bool> jsonTypeInfo))
		{
			jsonTypeInfo = JsonMetadataServices.CreateValueInfo<bool>(options, JsonMetadataServices.BooleanConverter);
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private JsonTypeInfo<Dictionary<string, object>> Create_DictionaryStringObject(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<Dictionary<string, object>> jsonTypeInfo))
		{
			JsonCollectionInfoValues<Dictionary<string, object>> info = new JsonCollectionInfoValues<Dictionary<string, object>>
			{
				ObjectCreator = () => new Dictionary<string, object>(),
				SerializeHandler = null
			};
			jsonTypeInfo = JsonMetadataServices.CreateDictionaryInfo<Dictionary<string, object>, string, object>(options, info);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private JsonTypeInfo<JsonDocument> Create_JsonDocument(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<JsonDocument> jsonTypeInfo))
		{
			jsonTypeInfo = JsonMetadataServices.CreateValueInfo<JsonDocument>(options, JsonMetadataServices.JsonDocumentConverter);
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private JsonTypeInfo<JsonElement> Create_JsonElement(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<JsonElement> jsonTypeInfo))
		{
			jsonTypeInfo = JsonMetadataServices.CreateValueInfo<JsonElement>(options, JsonMetadataServices.JsonElementConverter);
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private JsonTypeInfo<JsonElement?> Create_NullableJsonElement(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<JsonElement?> jsonTypeInfo))
		{
			JsonConverter converter = JsonMetadataServices.GetNullableConverter<JsonElement>(options);
			jsonTypeInfo = JsonMetadataServices.CreateValueInfo<JsonElement?>(options, converter);
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private JsonTypeInfo<CdpError> Create_CdpError(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<CdpError> jsonTypeInfo))
		{
			JsonObjectInfoValues<CdpError> objectInfo = new JsonObjectInfoValues<CdpError>
			{
				ObjectCreator = () => new CdpError(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => CdpErrorPropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = CdpErrorSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] CdpErrorPropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[2];
		JsonPropertyInfoValues<int> info0 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpError),
			Converter = null,
			Getter = (object obj) => ((CdpError)obj).Code,
			Setter = delegate(object obj, int value)
			{
				((CdpError)obj).Code = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Code",
			JsonPropertyName = "code"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<string> info1 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpError),
			Converter = null,
			Getter = (object obj) => ((CdpError)obj).Message,
			Setter = delegate(object obj, string? value)
			{
				((CdpError)obj).Message = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Message",
			JsonPropertyName = "message"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		return array;
	}

	private void CdpErrorSerializeHandler(Utf8JsonWriter writer, CdpError? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteNumber(PropName_code, value.Code);
		writer.WriteString(PropName_message, value.Message);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<CdpMessage> Create_CdpMessage(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<CdpMessage> jsonTypeInfo))
		{
			JsonObjectInfoValues<CdpMessage> objectInfo = new JsonObjectInfoValues<CdpMessage>
			{
				ObjectCreator = () => new CdpMessage(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => CdpMessagePropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = CdpMessageSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] CdpMessagePropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[4];
		JsonPropertyInfoValues<int> info0 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpMessage),
			Converter = null,
			Getter = (object obj) => ((CdpMessage)obj).Id,
			Setter = delegate(object obj, int value)
			{
				((CdpMessage)obj).Id = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Id",
			JsonPropertyName = "id"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<string> info1 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpMessage),
			Converter = null,
			Getter = (object obj) => ((CdpMessage)obj).Method,
			Setter = delegate(object obj, string? value)
			{
				((CdpMessage)obj).Method = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Method",
			JsonPropertyName = "method"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		JsonPropertyInfoValues<JsonElement?> info2 = new JsonPropertyInfoValues<JsonElement?>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpMessage),
			Converter = null,
			Getter = (object obj) => ((CdpMessage)obj).Params,
			Setter = delegate(object obj, JsonElement? value)
			{
				((CdpMessage)obj).Params = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Params",
			JsonPropertyName = "params"
		};
		array[2] = JsonMetadataServices.CreatePropertyInfo(options, info2);
		JsonPropertyInfoValues<string> info3 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpMessage),
			Converter = null,
			Getter = (object obj) => ((CdpMessage)obj).SessionId,
			Setter = delegate(object obj, string? value)
			{
				((CdpMessage)obj).SessionId = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "SessionId",
			JsonPropertyName = "sessionId"
		};
		array[3] = JsonMetadataServices.CreatePropertyInfo(options, info3);
		return array;
	}

	private void CdpMessageSerializeHandler(Utf8JsonWriter writer, CdpMessage? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteNumber(PropName_id, value.Id);
		writer.WriteString(PropName_method, value.Method);
		writer.WritePropertyName(PropName_params);
		JsonSerializer.Serialize(writer, value.Params, NullableJsonElement);
		writer.WriteString(PropName_sessionId, value.SessionId);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<CdpResponse> Create_CdpResponse(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<CdpResponse> jsonTypeInfo))
		{
			JsonObjectInfoValues<CdpResponse> objectInfo = new JsonObjectInfoValues<CdpResponse>
			{
				ObjectCreator = () => new CdpResponse(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => CdpResponsePropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = CdpResponseSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] CdpResponsePropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[6];
		JsonPropertyInfoValues<int> info0 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpResponse),
			Converter = null,
			Getter = (object obj) => ((CdpResponse)obj).Id,
			Setter = delegate(object obj, int value)
			{
				((CdpResponse)obj).Id = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Id",
			JsonPropertyName = "id"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<JsonElement> info1 = new JsonPropertyInfoValues<JsonElement>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpResponse),
			Converter = null,
			Getter = (object obj) => ((CdpResponse)obj).Result,
			Setter = delegate(object obj, JsonElement value)
			{
				((CdpResponse)obj).Result = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Result",
			JsonPropertyName = "result"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		JsonPropertyInfoValues<CdpError> info2 = new JsonPropertyInfoValues<CdpError>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpResponse),
			Converter = null,
			Getter = (object obj) => ((CdpResponse)obj).Error,
			Setter = delegate(object obj, CdpError? value)
			{
				((CdpResponse)obj).Error = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Error",
			JsonPropertyName = "error"
		};
		array[2] = JsonMetadataServices.CreatePropertyInfo(options, info2);
		JsonPropertyInfoValues<string> info3 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpResponse),
			Converter = null,
			Getter = (object obj) => ((CdpResponse)obj).Method,
			Setter = delegate(object obj, string? value)
			{
				((CdpResponse)obj).Method = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Method",
			JsonPropertyName = "method"
		};
		array[3] = JsonMetadataServices.CreatePropertyInfo(options, info3);
		JsonPropertyInfoValues<JsonElement?> info4 = new JsonPropertyInfoValues<JsonElement?>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpResponse),
			Converter = null,
			Getter = (object obj) => ((CdpResponse)obj).Params,
			Setter = delegate(object obj, JsonElement? value)
			{
				((CdpResponse)obj).Params = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Params",
			JsonPropertyName = "params"
		};
		array[4] = JsonMetadataServices.CreatePropertyInfo(options, info4);
		JsonPropertyInfoValues<string> info5 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(CdpResponse),
			Converter = null,
			Getter = (object obj) => ((CdpResponse)obj).SessionId,
			Setter = delegate(object obj, string? value)
			{
				((CdpResponse)obj).SessionId = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "SessionId",
			JsonPropertyName = "sessionId"
		};
		array[5] = JsonMetadataServices.CreatePropertyInfo(options, info5);
		return array;
	}

	private void CdpResponseSerializeHandler(Utf8JsonWriter writer, CdpResponse? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteNumber(PropName_id, value.Id);
		writer.WritePropertyName(PropName_result);
		JsonSerializer.Serialize(writer, value.Result, JsonElement);
		writer.WritePropertyName(PropName_error);
		CdpErrorSerializeHandler(writer, value.Error);
		writer.WriteString(PropName_method, value.Method);
		writer.WritePropertyName(PropName_params);
		JsonSerializer.Serialize(writer, value.Params, NullableJsonElement);
		writer.WriteString(PropName_sessionId, value.SessionId);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<ChromeTargetInfo> Create_ChromeTargetInfo(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<ChromeTargetInfo> jsonTypeInfo))
		{
			JsonObjectInfoValues<ChromeTargetInfo> objectInfo = new JsonObjectInfoValues<ChromeTargetInfo>
			{
				ObjectCreator = () => new ChromeTargetInfo(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => ChromeTargetInfoPropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = ChromeTargetInfoSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] ChromeTargetInfoPropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[5];
		JsonPropertyInfoValues<string> info0 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeTargetInfo),
			Converter = null,
			Getter = (object obj) => ((ChromeTargetInfo)obj).Id,
			Setter = delegate(object obj, string? value)
			{
				((ChromeTargetInfo)obj).Id = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Id",
			JsonPropertyName = "id"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<string> info1 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeTargetInfo),
			Converter = null,
			Getter = (object obj) => ((ChromeTargetInfo)obj).Type,
			Setter = delegate(object obj, string? value)
			{
				((ChromeTargetInfo)obj).Type = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Type",
			JsonPropertyName = "type"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		JsonPropertyInfoValues<string> info2 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeTargetInfo),
			Converter = null,
			Getter = (object obj) => ((ChromeTargetInfo)obj).Title,
			Setter = delegate(object obj, string? value)
			{
				((ChromeTargetInfo)obj).Title = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Title",
			JsonPropertyName = "title"
		};
		array[2] = JsonMetadataServices.CreatePropertyInfo(options, info2);
		JsonPropertyInfoValues<string> info3 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeTargetInfo),
			Converter = null,
			Getter = (object obj) => ((ChromeTargetInfo)obj).Url,
			Setter = delegate(object obj, string? value)
			{
				((ChromeTargetInfo)obj).Url = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Url",
			JsonPropertyName = "url"
		};
		array[3] = JsonMetadataServices.CreatePropertyInfo(options, info3);
		JsonPropertyInfoValues<string> info4 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeTargetInfo),
			Converter = null,
			Getter = (object obj) => ((ChromeTargetInfo)obj).WebSocketDebuggerUrl,
			Setter = delegate(object obj, string? value)
			{
				((ChromeTargetInfo)obj).WebSocketDebuggerUrl = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "WebSocketDebuggerUrl",
			JsonPropertyName = "webSocketDebuggerUrl"
		};
		array[4] = JsonMetadataServices.CreatePropertyInfo(options, info4);
		return array;
	}

	private void ChromeTargetInfoSerializeHandler(Utf8JsonWriter writer, ChromeTargetInfo? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteString(PropName_id, value.Id);
		writer.WriteString(PropName_type, value.Type);
		writer.WriteString(PropName_title, value.Title);
		writer.WriteString(PropName_url, value.Url);
		writer.WriteString(PropName_webSocketDebuggerUrl, value.WebSocketDebuggerUrl);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<ChromeTargetInfo[]> Create_ChromeTargetInfoArray(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<ChromeTargetInfo[]> jsonTypeInfo))
		{
			JsonCollectionInfoValues<ChromeTargetInfo[]> info = new JsonCollectionInfoValues<ChromeTargetInfo[]>
			{
				ObjectCreator = null,
				SerializeHandler = ChromeTargetInfoArraySerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateArrayInfo(options, info);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private void ChromeTargetInfoArraySerializeHandler(Utf8JsonWriter writer, ChromeTargetInfo[]? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartArray();
		for (int i = 0; i < value.Length; i++)
		{
			ChromeTargetInfoSerializeHandler(writer, value[i]);
		}
		writer.WriteEndArray();
	}

	private JsonTypeInfo<ChromeVersion> Create_ChromeVersion(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<ChromeVersion> jsonTypeInfo))
		{
			JsonObjectInfoValues<ChromeVersion> objectInfo = new JsonObjectInfoValues<ChromeVersion>
			{
				ObjectCreator = () => new ChromeVersion(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => ChromeVersionPropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = ChromeVersionSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] ChromeVersionPropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[2];
		JsonPropertyInfoValues<string> info0 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeVersion),
			Converter = null,
			Getter = (object obj) => ((ChromeVersion)obj).WebSocketDebuggerUrl,
			Setter = delegate(object obj, string? value)
			{
				((ChromeVersion)obj).WebSocketDebuggerUrl = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "WebSocketDebuggerUrl",
			JsonPropertyName = "webSocketDebuggerUrl"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<string> info1 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeVersion),
			Converter = null,
			Getter = (object obj) => ((ChromeVersion)obj).Browser,
			Setter = delegate(object obj, string? value)
			{
				((ChromeVersion)obj).Browser = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Browser",
			JsonPropertyName = "Browser"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		return array;
	}

	private void ChromeVersionSerializeHandler(Utf8JsonWriter writer, ChromeVersion? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteString(PropName_webSocketDebuggerUrl, value.WebSocketDebuggerUrl);
		writer.WriteString(PropName_Browser, value.Browser);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<ChromeVersionResponse> Create_ChromeVersionResponse(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<ChromeVersionResponse> jsonTypeInfo))
		{
			JsonObjectInfoValues<ChromeVersionResponse> objectInfo = new JsonObjectInfoValues<ChromeVersionResponse>
			{
				ObjectCreator = () => new ChromeVersionResponse(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => ChromeVersionResponsePropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = ChromeVersionResponseSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] ChromeVersionResponsePropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[6];
		JsonPropertyInfoValues<string> info0 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeVersionResponse),
			Converter = null,
			Getter = (object obj) => ((ChromeVersionResponse)obj).Browser,
			Setter = delegate(object obj, string? value)
			{
				((ChromeVersionResponse)obj).Browser = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Browser",
			JsonPropertyName = "Browser"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<string> info1 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeVersionResponse),
			Converter = null,
			Getter = (object obj) => ((ChromeVersionResponse)obj).ProtocolVersion,
			Setter = delegate(object obj, string? value)
			{
				((ChromeVersionResponse)obj).ProtocolVersion = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "ProtocolVersion",
			JsonPropertyName = "Protocol-Version"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		JsonPropertyInfoValues<string> info2 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeVersionResponse),
			Converter = null,
			Getter = (object obj) => ((ChromeVersionResponse)obj).UserAgent,
			Setter = delegate(object obj, string? value)
			{
				((ChromeVersionResponse)obj).UserAgent = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "UserAgent",
			JsonPropertyName = "User-Agent"
		};
		array[2] = JsonMetadataServices.CreatePropertyInfo(options, info2);
		JsonPropertyInfoValues<string> info3 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeVersionResponse),
			Converter = null,
			Getter = (object obj) => ((ChromeVersionResponse)obj).V8Version,
			Setter = delegate(object obj, string? value)
			{
				((ChromeVersionResponse)obj).V8Version = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "V8Version",
			JsonPropertyName = "V8-Version"
		};
		array[3] = JsonMetadataServices.CreatePropertyInfo(options, info3);
		JsonPropertyInfoValues<string> info4 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeVersionResponse),
			Converter = null,
			Getter = (object obj) => ((ChromeVersionResponse)obj).WebKitVersion,
			Setter = delegate(object obj, string? value)
			{
				((ChromeVersionResponse)obj).WebKitVersion = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "WebKitVersion",
			JsonPropertyName = "WebKit-Version"
		};
		array[4] = JsonMetadataServices.CreatePropertyInfo(options, info4);
		JsonPropertyInfoValues<string> info5 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(ChromeVersionResponse),
			Converter = null,
			Getter = (object obj) => ((ChromeVersionResponse)obj).WebSocketDebuggerUrl,
			Setter = delegate(object obj, string? value)
			{
				((ChromeVersionResponse)obj).WebSocketDebuggerUrl = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "WebSocketDebuggerUrl",
			JsonPropertyName = "webSocketDebuggerUrl"
		};
		array[5] = JsonMetadataServices.CreatePropertyInfo(options, info5);
		return array;
	}

	private void ChromeVersionResponseSerializeHandler(Utf8JsonWriter writer, ChromeVersionResponse? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteString(PropName_Browser, value.Browser);
		writer.WriteString(EncodedPropName_50726F746F636F6C2D56657273696F6E, value.ProtocolVersion);
		writer.WriteString(EncodedPropName_557365722D4167656E74, value.UserAgent);
		writer.WriteString(EncodedPropName_56382D56657273696F6E, value.V8Version);
		writer.WriteString(EncodedPropName_5765624B69742D56657273696F6E, value.WebKitVersion);
		writer.WriteString(PropName_webSocketDebuggerUrl, value.WebSocketDebuggerUrl);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<FirebaseUpdateInfo> Create_FirebaseUpdateInfo(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<FirebaseUpdateInfo> jsonTypeInfo))
		{
			JsonObjectInfoValues<FirebaseUpdateInfo> objectInfo = new JsonObjectInfoValues<FirebaseUpdateInfo>
			{
				ObjectCreator = () => new FirebaseUpdateInfo(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => FirebaseUpdateInfoPropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = FirebaseUpdateInfoSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] FirebaseUpdateInfoPropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[3];
		JsonPropertyInfoValues<string> info0 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(FirebaseUpdateInfo),
			Converter = null,
			Getter = (object obj) => ((FirebaseUpdateInfo)obj).Version,
			Setter = delegate(object obj, string? value)
			{
				((FirebaseUpdateInfo)obj).Version = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Version",
			JsonPropertyName = "version"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<string> info1 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(FirebaseUpdateInfo),
			Converter = null,
			Getter = (object obj) => ((FirebaseUpdateInfo)obj).DownloadUrl,
			Setter = delegate(object obj, string? value)
			{
				((FirebaseUpdateInfo)obj).DownloadUrl = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "DownloadUrl",
			JsonPropertyName = "downloadUrl"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		JsonPropertyInfoValues<string> info2 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(FirebaseUpdateInfo),
			Converter = null,
			Getter = (object obj) => ((FirebaseUpdateInfo)obj).Notes,
			Setter = delegate(object obj, string? value)
			{
				((FirebaseUpdateInfo)obj).Notes = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Notes",
			JsonPropertyName = "notes"
		};
		array[2] = JsonMetadataServices.CreatePropertyInfo(options, info2);
		return array;
	}

	private void FirebaseUpdateInfoSerializeHandler(Utf8JsonWriter writer, FirebaseUpdateInfo? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteString(PropName_version, value.Version);
		writer.WriteString(PropName_downloadUrl, value.DownloadUrl);
		writer.WriteString(PropName_notes, value.Notes);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<KeyAuthInitRequest> Create_KeyAuthInitRequest(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<KeyAuthInitRequest> jsonTypeInfo))
		{
			JsonObjectInfoValues<KeyAuthInitRequest> objectInfo = new JsonObjectInfoValues<KeyAuthInitRequest>
			{
				ObjectCreator = () => new KeyAuthInitRequest(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => KeyAuthInitRequestPropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = KeyAuthInitRequestSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] KeyAuthInitRequestPropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[6];
		JsonPropertyInfoValues<string> info0 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthInitRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthInitRequest)obj).Type,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthInitRequest)obj).Type = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Type",
			JsonPropertyName = "type"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<string> info1 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthInitRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthInitRequest)obj).Version,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthInitRequest)obj).Version = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Version",
			JsonPropertyName = "ver"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		JsonPropertyInfoValues<string> info2 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthInitRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthInitRequest)obj).Hash,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthInitRequest)obj).Hash = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Hash",
			JsonPropertyName = "hash"
		};
		array[2] = JsonMetadataServices.CreatePropertyInfo(options, info2);
		JsonPropertyInfoValues<string> info3 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthInitRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthInitRequest)obj).EncKey,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthInitRequest)obj).EncKey = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "EncKey",
			JsonPropertyName = "enckey"
		};
		array[3] = JsonMetadataServices.CreatePropertyInfo(options, info3);
		JsonPropertyInfoValues<string> info4 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthInitRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthInitRequest)obj).Name,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthInitRequest)obj).Name = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Name",
			JsonPropertyName = "name"
		};
		array[4] = JsonMetadataServices.CreatePropertyInfo(options, info4);
		JsonPropertyInfoValues<string> info5 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthInitRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthInitRequest)obj).OwnerId,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthInitRequest)obj).OwnerId = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "OwnerId",
			JsonPropertyName = "ownerid"
		};
		array[5] = JsonMetadataServices.CreatePropertyInfo(options, info5);
		return array;
	}

	private void KeyAuthInitRequestSerializeHandler(Utf8JsonWriter writer, KeyAuthInitRequest? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteString(PropName_type, value.Type);
		writer.WriteString(PropName_ver, value.Version);
		writer.WriteString(PropName_hash, value.Hash);
		writer.WriteString(PropName_enckey, value.EncKey);
		writer.WriteString(PropName_name, value.Name);
		writer.WriteString(PropName_ownerid, value.OwnerId);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<KeyAuthLoginRequest> Create_KeyAuthLoginRequest(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<KeyAuthLoginRequest> jsonTypeInfo))
		{
			JsonObjectInfoValues<KeyAuthLoginRequest> objectInfo = new JsonObjectInfoValues<KeyAuthLoginRequest>
			{
				ObjectCreator = () => new KeyAuthLoginRequest(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => KeyAuthLoginRequestPropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = KeyAuthLoginRequestSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] KeyAuthLoginRequestPropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[7];
		JsonPropertyInfoValues<string> info0 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthLoginRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthLoginRequest)obj).Type,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthLoginRequest)obj).Type = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Type",
			JsonPropertyName = "type"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<string> info1 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthLoginRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthLoginRequest)obj).Username,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthLoginRequest)obj).Username = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Username",
			JsonPropertyName = "username"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		JsonPropertyInfoValues<string> info2 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthLoginRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthLoginRequest)obj).Password,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthLoginRequest)obj).Password = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Password",
			JsonPropertyName = "pass"
		};
		array[2] = JsonMetadataServices.CreatePropertyInfo(options, info2);
		JsonPropertyInfoValues<string> info3 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthLoginRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthLoginRequest)obj).Hwid,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthLoginRequest)obj).Hwid = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Hwid",
			JsonPropertyName = "hwid"
		};
		array[3] = JsonMetadataServices.CreatePropertyInfo(options, info3);
		JsonPropertyInfoValues<string> info4 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthLoginRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthLoginRequest)obj).SessionId,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthLoginRequest)obj).SessionId = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "SessionId",
			JsonPropertyName = "sessionid"
		};
		array[4] = JsonMetadataServices.CreatePropertyInfo(options, info4);
		JsonPropertyInfoValues<string> info5 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthLoginRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthLoginRequest)obj).Name,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthLoginRequest)obj).Name = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Name",
			JsonPropertyName = "name"
		};
		array[5] = JsonMetadataServices.CreatePropertyInfo(options, info5);
		JsonPropertyInfoValues<string> info6 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthLoginRequest),
			Converter = null,
			Getter = (object obj) => ((KeyAuthLoginRequest)obj).OwnerId,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthLoginRequest)obj).OwnerId = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "OwnerId",
			JsonPropertyName = "ownerid"
		};
		array[6] = JsonMetadataServices.CreatePropertyInfo(options, info6);
		return array;
	}

	private void KeyAuthLoginRequestSerializeHandler(Utf8JsonWriter writer, KeyAuthLoginRequest? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteString(PropName_type, value.Type);
		writer.WriteString(PropName_username, value.Username);
		writer.WriteString(PropName_pass, value.Password);
		writer.WriteString(PropName_hwid, value.Hwid);
		writer.WriteString(PropName_sessionid, value.SessionId);
		writer.WriteString(PropName_name, value.Name);
		writer.WriteString(PropName_ownerid, value.OwnerId);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<KeyAuthResponse> Create_KeyAuthResponse(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<KeyAuthResponse> jsonTypeInfo))
		{
			JsonObjectInfoValues<KeyAuthResponse> objectInfo = new JsonObjectInfoValues<KeyAuthResponse>
			{
				ObjectCreator = () => new KeyAuthResponse(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => KeyAuthResponsePropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = KeyAuthResponseSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] KeyAuthResponsePropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[5];
		JsonPropertyInfoValues<bool> info0 = new JsonPropertyInfoValues<bool>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthResponse),
			Converter = null,
			Getter = (object obj) => ((KeyAuthResponse)obj).Success,
			Setter = delegate(object obj, bool value)
			{
				((KeyAuthResponse)obj).Success = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Success",
			JsonPropertyName = "success"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<string> info1 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthResponse),
			Converter = null,
			Getter = (object obj) => ((KeyAuthResponse)obj).Message,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthResponse)obj).Message = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Message",
			JsonPropertyName = "message"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		JsonPropertyInfoValues<string> info2 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthResponse),
			Converter = null,
			Getter = (object obj) => ((KeyAuthResponse)obj).SessionId,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthResponse)obj).SessionId = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "SessionId",
			JsonPropertyName = "sessionid"
		};
		array[2] = JsonMetadataServices.CreatePropertyInfo(options, info2);
		JsonPropertyInfoValues<string> info3 = new JsonPropertyInfoValues<string>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthResponse),
			Converter = null,
			Getter = (object obj) => ((KeyAuthResponse)obj).Nonce,
			Setter = delegate(object obj, string? value)
			{
				((KeyAuthResponse)obj).Nonce = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Nonce",
			JsonPropertyName = "nonce"
		};
		array[3] = JsonMetadataServices.CreatePropertyInfo(options, info3);
		JsonPropertyInfoValues<int> info4 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(KeyAuthResponse),
			Converter = null,
			Getter = (object obj) => ((KeyAuthResponse)obj).Code,
			Setter = delegate(object obj, int value)
			{
				((KeyAuthResponse)obj).Code = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Code",
			JsonPropertyName = "code"
		};
		array[4] = JsonMetadataServices.CreatePropertyInfo(options, info4);
		return array;
	}

	private void KeyAuthResponseSerializeHandler(Utf8JsonWriter writer, KeyAuthResponse? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteBoolean(PropName_success, value.Success);
		writer.WriteString(PropName_message, value.Message);
		writer.WriteString(PropName_sessionid, value.SessionId);
		writer.WriteString(PropName_nonce, value.Nonce);
		writer.WriteNumber(PropName_code, value.Code);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<PickPositionResult> Create_PickPositionResult(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<PickPositionResult> jsonTypeInfo))
		{
			JsonObjectInfoValues<PickPositionResult> objectInfo = new JsonObjectInfoValues<PickPositionResult>
			{
				ObjectCreator = () => new PickPositionResult(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => PickPositionResultPropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = PickPositionResultSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] PickPositionResultPropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[2];
		JsonPropertyInfoValues<int> info0 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(PickPositionResult),
			Converter = null,
			Getter = (object obj) => ((PickPositionResult)obj).X,
			Setter = delegate(object obj, int value)
			{
				((PickPositionResult)obj).X = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "X",
			JsonPropertyName = "x"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<int> info1 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(PickPositionResult),
			Converter = null,
			Getter = (object obj) => ((PickPositionResult)obj).Y,
			Setter = delegate(object obj, int value)
			{
				((PickPositionResult)obj).Y = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "Y",
			JsonPropertyName = "y"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		return array;
	}

	private void PickPositionResultSerializeHandler(Utf8JsonWriter writer, PickPositionResult? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteNumber(PropName_x, value.X);
		writer.WriteNumber(PropName_y, value.Y);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<SavedPositions> Create_SavedPositions(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<SavedPositions> jsonTypeInfo))
		{
			JsonObjectInfoValues<SavedPositions> objectInfo = new JsonObjectInfoValues<SavedPositions>
			{
				ObjectCreator = () => new SavedPositions(),
				ObjectWithParameterizedConstructorCreator = null,
				PropertyMetadataInitializer = (JsonSerializerContext _) => SavedPositionsPropInit(options),
				ConstructorParameterMetadataInitializer = null,
				SerializeHandler = SavedPositionsSerializeHandler
			};
			jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
			jsonTypeInfo.NumberHandling = null;
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private static JsonPropertyInfo[] SavedPositionsPropInit(JsonSerializerOptions options)
	{
		JsonPropertyInfo[] array = new JsonPropertyInfo[8];
		JsonPropertyInfoValues<int> info0 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(SavedPositions),
			Converter = null,
			Getter = (object obj) => ((SavedPositions)obj).TextboxX,
			Setter = delegate(object obj, int value)
			{
				((SavedPositions)obj).TextboxX = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "TextboxX",
			JsonPropertyName = "TextboxX"
		};
		array[0] = JsonMetadataServices.CreatePropertyInfo(options, info0);
		JsonPropertyInfoValues<int> info1 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(SavedPositions),
			Converter = null,
			Getter = (object obj) => ((SavedPositions)obj).TextboxY,
			Setter = delegate(object obj, int value)
			{
				((SavedPositions)obj).TextboxY = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "TextboxY",
			JsonPropertyName = "TextboxY"
		};
		array[1] = JsonMetadataServices.CreatePropertyInfo(options, info1);
		JsonPropertyInfoValues<int> info2 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(SavedPositions),
			Converter = null,
			Getter = (object obj) => ((SavedPositions)obj).EmojiButtonX,
			Setter = delegate(object obj, int value)
			{
				((SavedPositions)obj).EmojiButtonX = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "EmojiButtonX",
			JsonPropertyName = "EmojiButtonX"
		};
		array[2] = JsonMetadataServices.CreatePropertyInfo(options, info2);
		JsonPropertyInfoValues<int> info3 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(SavedPositions),
			Converter = null,
			Getter = (object obj) => ((SavedPositions)obj).EmojiButtonY,
			Setter = delegate(object obj, int value)
			{
				((SavedPositions)obj).EmojiButtonY = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "EmojiButtonY",
			JsonPropertyName = "EmojiButtonY"
		};
		array[3] = JsonMetadataServices.CreatePropertyInfo(options, info3);
		JsonPropertyInfoValues<int> info4 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(SavedPositions),
			Converter = null,
			Getter = (object obj) => ((SavedPositions)obj).StickerTabX,
			Setter = delegate(object obj, int value)
			{
				((SavedPositions)obj).StickerTabX = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "StickerTabX",
			JsonPropertyName = "StickerTabX"
		};
		array[4] = JsonMetadataServices.CreatePropertyInfo(options, info4);
		JsonPropertyInfoValues<int> info5 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(SavedPositions),
			Converter = null,
			Getter = (object obj) => ((SavedPositions)obj).StickerTabY,
			Setter = delegate(object obj, int value)
			{
				((SavedPositions)obj).StickerTabY = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "StickerTabY",
			JsonPropertyName = "StickerTabY"
		};
		array[5] = JsonMetadataServices.CreatePropertyInfo(options, info5);
		JsonPropertyInfoValues<int> info6 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(SavedPositions),
			Converter = null,
			Getter = (object obj) => ((SavedPositions)obj).StickerX,
			Setter = delegate(object obj, int value)
			{
				((SavedPositions)obj).StickerX = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "StickerX",
			JsonPropertyName = "StickerX"
		};
		array[6] = JsonMetadataServices.CreatePropertyInfo(options, info6);
		JsonPropertyInfoValues<int> info7 = new JsonPropertyInfoValues<int>
		{
			IsProperty = true,
			IsPublic = true,
			IsVirtual = false,
			DeclaringType = typeof(SavedPositions),
			Converter = null,
			Getter = (object obj) => ((SavedPositions)obj).StickerY,
			Setter = delegate(object obj, int value)
			{
				((SavedPositions)obj).StickerY = value;
			},
			IgnoreCondition = null,
			HasJsonInclude = false,
			IsExtensionData = false,
			NumberHandling = null,
			PropertyName = "StickerY",
			JsonPropertyName = "StickerY"
		};
		array[7] = JsonMetadataServices.CreatePropertyInfo(options, info7);
		return array;
	}

	private void SavedPositionsSerializeHandler(Utf8JsonWriter writer, SavedPositions? value)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}
		writer.WriteStartObject();
		writer.WriteNumber(PropName_TextboxX, value.TextboxX);
		writer.WriteNumber(PropName_TextboxY, value.TextboxY);
		writer.WriteNumber(PropName_EmojiButtonX, value.EmojiButtonX);
		writer.WriteNumber(PropName_EmojiButtonY, value.EmojiButtonY);
		writer.WriteNumber(PropName_StickerTabX, value.StickerTabX);
		writer.WriteNumber(PropName_StickerTabY, value.StickerTabY);
		writer.WriteNumber(PropName_StickerX, value.StickerX);
		writer.WriteNumber(PropName_StickerY, value.StickerY);
		writer.WriteEndObject();
	}

	private JsonTypeInfo<int> Create_Int32(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<int> jsonTypeInfo))
		{
			jsonTypeInfo = JsonMetadataServices.CreateValueInfo<int>(options, JsonMetadataServices.Int32Converter);
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private JsonTypeInfo<object> Create_Object(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<object> jsonTypeInfo))
		{
			jsonTypeInfo = JsonMetadataServices.CreateValueInfo<object>(options, JsonMetadataServices.ObjectConverter);
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	private JsonTypeInfo<string> Create_String(JsonSerializerOptions options)
	{
		if (!TryGetTypeInfoForRuntimeCustomConverter(options, out JsonTypeInfo<string> jsonTypeInfo))
		{
			jsonTypeInfo = JsonMetadataServices.CreateValueInfo<string>(options, JsonMetadataServices.StringConverter);
		}
		jsonTypeInfo.OriginatingResolver = this;
		return jsonTypeInfo;
	}

	public AppJsonContext()
		: base(null)
	{
	}

	public AppJsonContext(JsonSerializerOptions options)
		: base(options)
	{
	}

	private static bool TryGetTypeInfoForRuntimeCustomConverter<TJsonMetadataType>(JsonSerializerOptions options, out JsonTypeInfo<TJsonMetadataType> jsonTypeInfo)
	{
		JsonConverter converter = GetRuntimeConverterForType(typeof(TJsonMetadataType), options);
		if (converter != null)
		{
			jsonTypeInfo = JsonMetadataServices.CreateValueInfo<TJsonMetadataType>(options, converter);
			return true;
		}
		jsonTypeInfo = null;
		return false;
	}

	private static JsonConverter? GetRuntimeConverterForType(Type type, JsonSerializerOptions options)
	{
		for (int i = 0; i < options.Converters.Count; i++)
		{
			JsonConverter converter = options.Converters[i];
			if (converter != null && converter.CanConvert(type))
			{
				return ExpandConverter(type, converter, options, validateCanConvert: false);
			}
		}
		return null;
	}

	private static JsonConverter ExpandConverter(Type type, JsonConverter converter, JsonSerializerOptions options, bool validateCanConvert = true)
	{
		if (validateCanConvert && !converter.CanConvert(type))
		{
			throw new InvalidOperationException($"The converter '{converter.GetType()}' is not compatible with the type '{type}'.");
		}
		if (converter is JsonConverterFactory factory)
		{
			converter = factory.CreateConverter(type, options);
			if (converter == null || converter is JsonConverterFactory)
			{
				throw new InvalidOperationException($"The converter '{factory.GetType()}' cannot return null or a JsonConverterFactory instance.");
			}
		}
		return converter;
	}

	public override JsonTypeInfo? GetTypeInfo(Type type)
	{
		base.Options.TryGetTypeInfo(type, out JsonTypeInfo typeInfo);
		return typeInfo;
	}

	JsonTypeInfo? IJsonTypeInfoResolver.GetTypeInfo(Type type, JsonSerializerOptions options)
	{
		if (type == typeof(bool))
		{
			return Create_Boolean(options);
		}
		if (type == typeof(Dictionary<string, object>))
		{
			return Create_DictionaryStringObject(options);
		}
		if (type == typeof(JsonDocument))
		{
			return Create_JsonDocument(options);
		}
		if (type == typeof(JsonElement))
		{
			return Create_JsonElement(options);
		}
		if (type == typeof(JsonElement?))
		{
			return Create_NullableJsonElement(options);
		}
		if (type == typeof(CdpError))
		{
			return Create_CdpError(options);
		}
		if (type == typeof(CdpMessage))
		{
			return Create_CdpMessage(options);
		}
		if (type == typeof(CdpResponse))
		{
			return Create_CdpResponse(options);
		}
		if (type == typeof(ChromeTargetInfo))
		{
			return Create_ChromeTargetInfo(options);
		}
		if (type == typeof(ChromeTargetInfo[]))
		{
			return Create_ChromeTargetInfoArray(options);
		}
		if (type == typeof(ChromeVersion))
		{
			return Create_ChromeVersion(options);
		}
		if (type == typeof(ChromeVersionResponse))
		{
			return Create_ChromeVersionResponse(options);
		}
		if (type == typeof(FirebaseUpdateInfo))
		{
			return Create_FirebaseUpdateInfo(options);
		}
		if (type == typeof(KeyAuthInitRequest))
		{
			return Create_KeyAuthInitRequest(options);
		}
		if (type == typeof(KeyAuthLoginRequest))
		{
			return Create_KeyAuthLoginRequest(options);
		}
		if (type == typeof(KeyAuthResponse))
		{
			return Create_KeyAuthResponse(options);
		}
		if (type == typeof(PickPositionResult))
		{
			return Create_PickPositionResult(options);
		}
		if (type == typeof(SavedPositions))
		{
			return Create_SavedPositions(options);
		}
		if (type == typeof(int))
		{
			return Create_Int32(options);
		}
		if (type == typeof(object))
		{
			return Create_Object(options);
		}
		if (type == typeof(string))
		{
			return Create_String(options);
		}
		return null;
	}
}
