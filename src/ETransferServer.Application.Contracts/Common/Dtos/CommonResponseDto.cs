using System;
using AutoResponseWrapper.Response;
using JetBrains.Annotations;
using Orleans;
using Volo.Abp;

namespace ETransferServer.Common.Dtos;

[GenerateSerializer]
public class CommonResponseDto<T> where T : class
{
    private const string SuccessCode = "20000";
    private const string CommonErrorCode = "50000";
    
    [Id(0)] public string Code { get; set; }
    [Id(1)] public object Data { get; set; }
    [Id(2)] public string Message { get; set; } = string.Empty;
    public bool Success => Code == SuccessCode;
    public T Value => Data as T;


    public CommonResponseDto()
    {
        Code = SuccessCode;
    }
    
    public CommonResponseDto(T data)
    {
        Code = SuccessCode;
        Data = data;
    }
    
    public CommonResponseDto<T> Error(string code, string message)
    {
        Code = code;
        Message = message;
        return this;
    }
    
    public CommonResponseDto<T> Error(string message)
    {
        Code = CommonErrorCode;
        Message = message;
        return this;
    }
    
    public CommonResponseDto<T> Error(Exception e, [CanBeNull] string message = null)
    {
        return e is UserFriendlyException ufe
            ? Error(ufe.Code, message ?? ufe.Message)
            : Error(CommonErrorCode, message ?? e.Message);
    }

}