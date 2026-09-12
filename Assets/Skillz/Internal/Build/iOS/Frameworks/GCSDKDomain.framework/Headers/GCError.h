/*
 * © 2012-2023 GeoComply Solutions Inc.
 * All Rights Reserved.
 * NOTICE: All information contained herein is, and remains
 * the property of GeoComply Solutions Inc.
 * Dissemination, distribution, copying of this information or reproduction
 * of this material is strictly forbidden unless prior written permission
 * is obtained from GeoComply Solutions Inc.
 */

#import <Foundation/Foundation.h>

/**
 These constant indicate the type of error that resulted after a geolocation request.
 */
typedef NS_ENUM(NSInteger, GCClientError) {
    GCCErrorNone                      = 0,
    
    /**
     Indicates that an unknown error occurs.
     */
    GCCErrorUnknown                   = 600,
    
    /**
     Indicates a network connection error.
     */
    GCCErrorNetworkConnection         = 602,
    
    /**
     Indicates an error when communicating with GeoComply Server.
     */
    GCCErrorServerCommunication       = 603,
    
    /**
     Indicates that this client has been suspended.
     */
    GCCErrorClientSuspended           = 604,
    
    /**
     Indicates that GeoComply iOS SDK solution has been disabled.
     */
    GCCErrorDisabledSolution          = 605,
    
    /**
     Indicates that license string has invalid format.
     */
    GCCErrorInvalidLicenseFormat      = 606,
    
    /**
     Indicates that Client's license is unauthorized.
     */
    GCCErrorClientLicenseUnauthorized = 607,
    
    /**
     Indicates that Client's license is expired.
     */
    GCCErrorLicenseExpired            = 608,
    
    /**
     Indicates that Custom Field(s) is(are) invalid.
     */
    GCCErrorInvalidCustomFields       = 609,
    
    /**
     Indicates that Cancel Geoloation is failed
     */
    GCCErrorRequestCancelledByApplication       = 610,
    
    /**
     Indicates that Location Service is turn off and geolocation request is cancelled.
     */
    GCCErrorRequestCanceled           = 611,
    
    /**
     Indicates that device's memory is low.
     */
    GCCErrorLowMemory                 = 612,
    
    /**
     Indicates that a geolocation request is in progress.
     */
    GCCErrorGeolocationInProgress     = 614,
    
    /**
     Indicates that GeoComply iOS SDK is updating indoor location.
     */
    GCCErrorIsUpdatingLocation        = 630,
    
    /**
     Indicates that the user granted the WhileInUse so cannot geolocate in the background mode.
     */
    GCCErrorPermissionWhileInUseInBackgroundMode            = 633,
    
    /**
     Indicates that application did enter background.
     */
    GCCErrorBackgroundMode            = 634,
    
    /**
     Indicates that input for User ID / Phone Number / Geolocation Reason / Custom Fields is (are) in invalid format.
     */
    GCCErrorInvalidInput              = 635,
    
    /**
     Indicates that the app has not included all external accessory protocols.
     */
    GCCErrorNotContainAllProtocols    = 636,
    
    /**
     Indicates that the app has turned off precise accuracy location in Settings app.
     */
    GCCErrorPreciseLocationIsOff      = 637,
    
    /**
     Indicates that the geolocation request is missing reason code when ios_requires_reason_code is on.
     */
    GCCErrorReasonCode         = 639,
    
    /**
     Indicates that the geolocation request has been modified during submission by another party.
     */
    GCCErrorInvalidHMAC               = 640,
    
    /**
     Indicates that the Status system is down.
     */
    GCCErrorStatusSystemIsDown          = 643,
    
    /**
     Indicates that the Status Service returns GeoComply is alive.
     */
    GCCErrorEngineStatusIsInconsistent  = 644,
    
    /**
     Indicates that the Carbon got an invalid API Key.
     */
    GCCErrorInvalidCarbonAPIKey  = 645,
    
    /**
     Indicates that the Carbon got an invalid Carbon Url.
     */
    GCCErrorInvalidCarbonUrl  = 646,
};


/**
 The GCError object manages information about the details of a failed geolocation.
 
 GCError will be return via -didGeolocationFailed: delegate method.
 
 @see -didGeolocationFailed:
 */
@interface GCError: NSObject

/**
 Returns error code when a geolocation request failed.
 
 @see GCClientError
 */
@property (nonatomic, readonly) GCClientError code;

/**
 Returns description when a geolocation request failed.
 */
@property (nonatomic, readonly, nullable) NSString* description;

/**
 A determination flag for app to retry calling a geolocation request.
 */
@property (nonatomic, readonly) BOOL shouldRetry;

@end

