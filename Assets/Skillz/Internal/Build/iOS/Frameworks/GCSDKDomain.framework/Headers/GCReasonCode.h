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

@interface GCReasonCode : NSObject
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull login;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull preWager;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull interval;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull ipChange;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull userDrivenRetry;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull postIntervalForeground;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull gameLaunchOrSwitch;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull registration;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangesNameAdd;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangesNameChange;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangesNameRemove;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangePhoneAdd;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangePhoneChange;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangePhoneRemove;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangeEmailAdd;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangeEmailChange;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangeEmailRemove;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangePassword;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangeSecurityAdd;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangeSecurityRemove;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangePhysicalAddressAdd;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangePhysicalAddressChange;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangePhysicalAddressRemove;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangeContactsAdd;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangeContactsChange;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull accountChangeContactsRemove;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull paymentInformationAddFundingMethod;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull paymentInformationChangeFundingMethod;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull paymentInformationRemoveFundingMethod;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull paymentInformationAddWithdrawalMethod;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull paymentInformationChangeWithdrawalMethod;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull paymentInformationRemoveWithdrawalMethod;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionDeposit;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionWithdrawal;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionPurchaseOrWager;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionSell;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionTransferSameUser;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionTransferDifferentUser;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionNavigationBalance;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionNavigationTransactionHistory;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionNavigationTransactionDetails;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionNavigationProfile;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionNavigationHelpCenter;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionNavigationTermsOfUse;
@property (class, nonatomic, strong, readonly) GCReasonCode * _Nonnull transactionNavigationContacts;

@property (nonatomic, strong, readonly) NSString * _Nullable code;
@property (nonatomic, strong, readonly) NSString * _Nonnull name;

+ (NSArray<GCReasonCode *> * _Nonnull)supportedReasonCodes;

+ (GCReasonCode * _Nullable)reasonCodeWithCode:(NSString * _Nullable)code;

- (instancetype _Nonnull)init NS_UNAVAILABLE;
+ (instancetype _Nonnull)new NS_UNAVAILABLE;

@end
