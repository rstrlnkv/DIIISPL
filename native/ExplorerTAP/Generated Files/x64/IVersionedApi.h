

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0628 */
/* at Tue Jan 19 06:14:07 2038
 */
/* Compiler settings for IVersionedApi.idl:
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

#ifndef COM_NO_WINDOWS_H
#include "windows.h"
#include "ole2.h"
#endif /*COM_NO_WINDOWS_H*/

#ifndef __IVersionedApi_h__
#define __IVersionedApi_h__

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

#ifndef __IVersionedApi_FWD_DEFINED__
#define __IVersionedApi_FWD_DEFINED__
typedef interface IVersionedApi IVersionedApi;

#endif 	/* __IVersionedApi_FWD_DEFINED__ */


/* header files for imported files */
#include "unknwn.h"

#ifdef __cplusplus
extern "C"{
#endif 


#ifndef __IVersionedApi_INTERFACE_DEFINED__
#define __IVersionedApi_INTERFACE_DEFINED__

/* interface IVersionedApi */
/* [uuid][object] */ 


EXTERN_C const IID IID_IVersionedApi;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("e0811f94-c7fd-4895-9df6-297b71e81e33")
    IVersionedApi : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetVersion( 
            /* [out] */ DWORD *apiVersion) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IVersionedApiVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            IVersionedApi * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            IVersionedApi * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            IVersionedApi * This);
        
        DECLSPEC_XFGVIRT(IVersionedApi, GetVersion)
        HRESULT ( STDMETHODCALLTYPE *GetVersion )( 
            IVersionedApi * This,
            /* [out] */ DWORD *apiVersion);
        
        END_INTERFACE
    } IVersionedApiVtbl;

    interface IVersionedApi
    {
        CONST_VTBL struct IVersionedApiVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IVersionedApi_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IVersionedApi_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IVersionedApi_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IVersionedApi_GetVersion(This,apiVersion)	\
    ( (This)->lpVtbl -> GetVersion(This,apiVersion) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IVersionedApi_INTERFACE_DEFINED__ */


/* Additional Prototypes for ALL interfaces */

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


