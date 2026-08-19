

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0628 */
/* at Tue Jan 19 06:14:07 2038
 */
/* Compiler settings for ITaskbarAppearanceService.idl:
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

#ifndef __ITaskbarAppearanceService_h__
#define __ITaskbarAppearanceService_h__

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

#ifndef __ITaskbarAppearanceService_FWD_DEFINED__
#define __ITaskbarAppearanceService_FWD_DEFINED__
typedef interface ITaskbarAppearanceService ITaskbarAppearanceService;

#endif 	/* __ITaskbarAppearanceService_FWD_DEFINED__ */


/* header files for imported files */
#include "unknwn.h"
#include "TaskbarBrush.h"

#ifdef __cplusplus
extern "C"{
#endif 


#ifndef __ITaskbarAppearanceService_INTERFACE_DEFINED__
#define __ITaskbarAppearanceService_INTERFACE_DEFINED__

/* interface ITaskbarAppearanceService */
/* [uuid][object] */ 


EXTERN_C const IID IID_ITaskbarAppearanceService;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("d255d70a-b6f7-4b66-9fc6-b290a598a1d3")
    ITaskbarAppearanceService : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE SetTaskbarAppearance( 
            /* [in] */ HWND taskbar,
            /* [in] */ enum TaskbarBrush brush,
            /* [in] */ UINT color) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetTaskbarBlur( 
            /* [in] */ HWND taskbar,
            /* [in] */ UINT color,
            /* [in] */ FLOAT blurAmount) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ReturnTaskbarToDefaultAppearance( 
            /* [in] */ HWND taskbar) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetTaskbarBorderVisibility( 
            /* [in] */ HWND taskbar,
            /* [in] */ BOOL visible) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE RestoreAllTaskbarsToDefault( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE RestoreAllTaskbarsToDefaultWhenProcessDies( 
            /* [in] */ DWORD pid) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE KillExplorerWhenPackageUninstalls( 
            /* [string][in] */ LPCWSTR packageFullName) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ITaskbarAppearanceServiceVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ITaskbarAppearanceService * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ITaskbarAppearanceService * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ITaskbarAppearanceService * This);
        
        DECLSPEC_XFGVIRT(ITaskbarAppearanceService, SetTaskbarAppearance)
        HRESULT ( STDMETHODCALLTYPE *SetTaskbarAppearance )( 
            ITaskbarAppearanceService * This,
            /* [in] */ HWND taskbar,
            /* [in] */ enum TaskbarBrush brush,
            /* [in] */ UINT color);
        
        DECLSPEC_XFGVIRT(ITaskbarAppearanceService, SetTaskbarBlur)
        HRESULT ( STDMETHODCALLTYPE *SetTaskbarBlur )( 
            ITaskbarAppearanceService * This,
            /* [in] */ HWND taskbar,
            /* [in] */ UINT color,
            /* [in] */ FLOAT blurAmount);
        
        DECLSPEC_XFGVIRT(ITaskbarAppearanceService, ReturnTaskbarToDefaultAppearance)
        HRESULT ( STDMETHODCALLTYPE *ReturnTaskbarToDefaultAppearance )( 
            ITaskbarAppearanceService * This,
            /* [in] */ HWND taskbar);
        
        DECLSPEC_XFGVIRT(ITaskbarAppearanceService, SetTaskbarBorderVisibility)
        HRESULT ( STDMETHODCALLTYPE *SetTaskbarBorderVisibility )( 
            ITaskbarAppearanceService * This,
            /* [in] */ HWND taskbar,
            /* [in] */ BOOL visible);
        
        DECLSPEC_XFGVIRT(ITaskbarAppearanceService, RestoreAllTaskbarsToDefault)
        HRESULT ( STDMETHODCALLTYPE *RestoreAllTaskbarsToDefault )( 
            ITaskbarAppearanceService * This);
        
        DECLSPEC_XFGVIRT(ITaskbarAppearanceService, RestoreAllTaskbarsToDefaultWhenProcessDies)
        HRESULT ( STDMETHODCALLTYPE *RestoreAllTaskbarsToDefaultWhenProcessDies )( 
            ITaskbarAppearanceService * This,
            /* [in] */ DWORD pid);
        
        DECLSPEC_XFGVIRT(ITaskbarAppearanceService, KillExplorerWhenPackageUninstalls)
        HRESULT ( STDMETHODCALLTYPE *KillExplorerWhenPackageUninstalls )( 
            ITaskbarAppearanceService * This,
            /* [string][in] */ LPCWSTR packageFullName);
        
        END_INTERFACE
    } ITaskbarAppearanceServiceVtbl;

    interface ITaskbarAppearanceService
    {
        CONST_VTBL struct ITaskbarAppearanceServiceVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ITaskbarAppearanceService_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ITaskbarAppearanceService_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ITaskbarAppearanceService_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ITaskbarAppearanceService_SetTaskbarAppearance(This,taskbar,brush,color)	\
    ( (This)->lpVtbl -> SetTaskbarAppearance(This,taskbar,brush,color) ) 

#define ITaskbarAppearanceService_SetTaskbarBlur(This,taskbar,color,blurAmount)	\
    ( (This)->lpVtbl -> SetTaskbarBlur(This,taskbar,color,blurAmount) ) 

#define ITaskbarAppearanceService_ReturnTaskbarToDefaultAppearance(This,taskbar)	\
    ( (This)->lpVtbl -> ReturnTaskbarToDefaultAppearance(This,taskbar) ) 

#define ITaskbarAppearanceService_SetTaskbarBorderVisibility(This,taskbar,visible)	\
    ( (This)->lpVtbl -> SetTaskbarBorderVisibility(This,taskbar,visible) ) 

#define ITaskbarAppearanceService_RestoreAllTaskbarsToDefault(This)	\
    ( (This)->lpVtbl -> RestoreAllTaskbarsToDefault(This) ) 

#define ITaskbarAppearanceService_RestoreAllTaskbarsToDefaultWhenProcessDies(This,pid)	\
    ( (This)->lpVtbl -> RestoreAllTaskbarsToDefaultWhenProcessDies(This,pid) ) 

#define ITaskbarAppearanceService_KillExplorerWhenPackageUninstalls(This,packageFullName)	\
    ( (This)->lpVtbl -> KillExplorerWhenPackageUninstalls(This,packageFullName) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ITaskbarAppearanceService_INTERFACE_DEFINED__ */


/* Additional Prototypes for ALL interfaces */

unsigned long             __RPC_USER  HWND_UserSize(     unsigned long *, unsigned long            , HWND * ); 
unsigned char * __RPC_USER  HWND_UserMarshal(  unsigned long *, unsigned char *, HWND * ); 
unsigned char * __RPC_USER  HWND_UserUnmarshal(unsigned long *, unsigned char *, HWND * ); 
void                      __RPC_USER  HWND_UserFree(     unsigned long *, HWND * ); 

unsigned long             __RPC_USER  HWND_UserSize64(     unsigned long *, unsigned long            , HWND * ); 
unsigned char * __RPC_USER  HWND_UserMarshal64(  unsigned long *, unsigned char *, HWND * ); 
unsigned char * __RPC_USER  HWND_UserUnmarshal64(unsigned long *, unsigned char *, HWND * ); 
void                      __RPC_USER  HWND_UserFree64(     unsigned long *, HWND * ); 

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


