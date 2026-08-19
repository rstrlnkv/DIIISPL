

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0628 */
/* at Tue Jan 19 06:14:07 2038
 */
/* Compiler settings for ExplorerTAP.idl:
    Oicf, W1, Zp8, env=Win64 (32b run), target_arch=AMD64 8.01.0628 
    protocol : all , ms_ext, c_ext, robust
    error checks: allocation ref bounds_check enum stub_data 
    VC __declspec() decoration level: 
         __declspec(uuid()), __declspec(selectany), __declspec(novtable)
         DECLSPEC_UUID(), MIDL_INTERFACE()
*/
/* @@MIDL_FILE_HEADING(  ) */



/* verify that the <rpcndr.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCNDR_H_VERSION__
#define __REQUIRED_RPCNDR_H_VERSION__ 500
#endif

#include "rpc.h"
#include "rpcndr.h"

#ifndef __RPCNDR_H_VERSION__
#error this stub requires an updated version of <rpcndr.h>
#endif /* __RPCNDR_H_VERSION__ */


#ifndef __ExplorerTAP_h__
#define __ExplorerTAP_h__

#if defined(_MSC_VER) && (_MSC_VER >= 1020)
#pragma once
#endif

#ifndef DECLSPEC_XFGVIRT
#if defined(_CONTROL_FLOW_GUARD_XFG)
#define DECLSPEC_XFGVIRT(base, func) __declspec(xfg_virtual(base, func))
#else
#define DECLSPEC_XFGVIRT(base, func)
#endif
#endif

/* Forward Declarations */ 

#ifndef __TaskbarAppearanceService_FWD_DEFINED__
#define __TaskbarAppearanceService_FWD_DEFINED__

#ifdef __cplusplus
typedef class TaskbarAppearanceService TaskbarAppearanceService;
#else
typedef struct TaskbarAppearanceService TaskbarAppearanceService;
#endif /* __cplusplus */

#endif 	/* __TaskbarAppearanceService_FWD_DEFINED__ */


#ifndef __TAPSite_FWD_DEFINED__
#define __TAPSite_FWD_DEFINED__

#ifdef __cplusplus
typedef class TAPSite TAPSite;
#else
typedef struct TAPSite TAPSite;
#endif /* __cplusplus */

#endif 	/* __TAPSite_FWD_DEFINED__ */


/* header files for imported files */
#include "ITaskbarAppearanceService.h"
#include "IVersionedApi.h"
#include "ocidl.h"

#ifdef __cplusplus
extern "C"{
#endif 



#ifndef __ExplorerTAP_LIBRARY_DEFINED__
#define __ExplorerTAP_LIBRARY_DEFINED__

/* library ExplorerTAP */
/* [uuid] */ 


EXTERN_C const IID LIBID_ExplorerTAP;

EXTERN_C const CLSID CLSID_TaskbarAppearanceService;

#ifdef __cplusplus

class DECLSPEC_UUID("5a6ba2a9-37cb-4ed4-91bd-012125ecc43d")
TaskbarAppearanceService;
#endif

EXTERN_C const CLSID CLSID_TAPSite;

#ifdef __cplusplus

class DECLSPEC_UUID("9d1cff9e-0388-4220-a4f4-3851a6ae3314")
TAPSite;
#endif
#endif /* __ExplorerTAP_LIBRARY_DEFINED__ */

/* Additional Prototypes for ALL interfaces */

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


