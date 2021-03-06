#pragma once
//-------------------------------------------------------------------------------------------------
// <copyright file="apuputil.h" company="Microsoft">
//    Copyright (c) Microsoft Corporation.  All rights reserved.
//    
//    The use and distribution terms for this software are covered by the
//    Common Public License 1.0 (http://opensource.org/licenses/cpl1.0.php)
//    which can be found in the file CPL.TXT at the root of this distribution.
//    By using this software in any fashion, you are agreeing to be bound by
//    the terms of this license.
//    
//    You must not remove this notice, or any other, from this software.
// </copyright>
// 
// <summary>
//    Header for Application Update helper functions.
// </summary>
//-------------------------------------------------------------------------------------------------

#ifdef __cplusplus
extern "C" {
#endif

#define ReleaseApupChain(p) if (p) { ApupFreeChain(p); p = NULL; }
#define ReleaseNullApupChain(p) if (p) { ApupFreeChain(p); p = NULL; }


const LPCWSTR APPLICATION_SYNDICATION_NAMESPACE = L"http://appsyndication.org/2006/appsyn";

enum APUP_HASH_ALGORITHM
{
    APUP_HASH_ALGORITHM_UNKNOWN,
    APUP_HASH_ALGORITHM_MD5,
    APUP_HASH_ALGORITHM_SHA1,
    APUP_HASH_ALGORITHM_SHA256,
};


struct APPLICATION_UPDATE_ENCLOSURE
{
    LPWSTR wzUrl;
    LPWSTR wzLocalName;
    DWORD64 dw64Size;

    BYTE* rgbDigest;
    DWORD cbDigest;
    APUP_HASH_ALGORITHM digestAlgorithm;

    BOOL fInstaller;
};


struct APPLICATION_UPDATE_ENTRY
{
    LPWSTR wzApplicationId;
    LPWSTR wzApplicationType;

    DWORD64 dw64Version;
    LPWSTR wzUpgradeId;
    DWORD64 dw64UpgradeVersion;
    BOOL fUpgradeExclusive;

    DWORD64 dw64TotalSize;

    DWORD cEnclosures;
    APPLICATION_UPDATE_ENCLOSURE* rgEnclosures;
};


struct APPLICATION_UPDATE_CHAIN
{
    LPWSTR wzDefaultApplicationId;
    LPWSTR wzDefaultApplicationType;

    DWORD cEntries;
    APPLICATION_UPDATE_ENTRY* rgEntries;
};


HRESULT DAPI ApupAllocChainFromAtom(
    __in ATOM_FEED* pFeed,
    __out APPLICATION_UPDATE_CHAIN** ppChain
    );

HRESULT DAPI ApupFilterChain(
    __in APPLICATION_UPDATE_CHAIN* pChain,
    __in DWORD64 dw64Version,
    __out APPLICATION_UPDATE_CHAIN** ppFilteredChain
    );

void DAPI ApupFreeChain(
    __in APPLICATION_UPDATE_CHAIN* pChain
    );

#ifdef __cplusplus
}
#endif
