#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>
#import <LocalAuthentication/LocalAuthentication.h>
#import <LocalAuthenticationEmbeddedUI/LocalAuthenticationEmbeddedUI.h>
#include <stdint.h>

typedef void (*TouchIdReply)(int32_t request, int32_t result);
static LAContext *activeContext;
static NSView *authenticationView;
static NSRect screenViewRect;
static uint64_t generation;
static int32_t activeRequest;
static TouchIdReply activeReply;

__attribute__((visibility("default"))) int32_t Team1TouchIdBridgeVersion(void) { return 4; }

static void OnMain(dispatch_block_t block) {
    if ([NSThread isMainThread]) block();
    else dispatch_async(dispatch_get_main_queue(), block);
}

static void RemoveView(void) {
    [authenticationView removeFromSuperview];
    authenticationView = nil;
}

static void PlaceView(void) {
    if (!authenticationView) return;
    NSWindow *window = authenticationView.window ?: NSApp.keyWindow ?: NSApp.mainWindow;
    if (!window) {
        for (NSWindow *candidate in NSApp.orderedWindows) {
            if (candidate.isVisible && candidate.contentView) { window=candidate; break; }
        }
    }
    if (!window) return;
    NSRect bounds=window.contentView.bounds;
    CGFloat bottom=window.contentView.isFlipped?NSMaxY(bounds)-52:NSMinY(bounds)+24;
    NSRect fallback=NSMakeRect(NSMidX(bounds)-14,bottom,28,28);
    NSRect global = screenViewRect;
    if (NSWidth(global) > 0 && NSScreen.screens.count > 0) {
        global.origin.y = NSMaxY(NSScreen.screens.firstObject.frame) - NSMaxY(global);
        NSPoint center = NSMakePoint(NSMidX(global), NSMidY(global));
        if (!NSPointInRect(center, window.frame)) {
            for (NSWindow *candidate in NSApp.orderedWindows) {
                if (candidate.isVisible && candidate.contentView && NSPointInRect(center, candidate.frame)) {
                    window = candidate;
                    break;
                }
            }
        }
        NSRect local = [window convertRectFromScreen:global];
        local = [window.contentView convertRect:local fromView:nil];
        // Editor の Retina/ゲームビュー座標が食い違っても、認証表示を画面外に置かない。
        authenticationView.frame=NSContainsRect(window.contentView.bounds,local)?local:fallback;
    } else {
        authenticationView.frame=fallback;
    }
    if (authenticationView.superview != window.contentView)
        [window.contentView addSubview:authenticationView positioned:NSWindowAbove relativeTo:nil];
}

__attribute__((visibility("default"))) void Team1TouchIdSetViewRect(double x, double y, double width, double height) {
    OnMain(^{ screenViewRect=NSMakeRect(x,y,width,height); PlaceView(); });
}

// Availability: 0 = system authentication ready; 1 = unavailable; 2/3 = enrollment/lockout.
__attribute__((visibility("default"))) int32_t Team1TouchIdAvailability(void) {
    @autoreleasepool {
        LAContext *context=[LAContext new]; NSError *error=nil;
        if ([context canEvaluatePolicy:LAPolicyDeviceOwnerAuthentication error:&error]) return 0;
        if (error.code==LAErrorBiometryNotEnrolled) return 2;
        if (error.code==LAErrorBiometryLockout) return 3;
        return 1;
    }
}

__attribute__((visibility("default"))) void Team1TouchIdCancel(void) {
    OnMain(^{ generation++; activeReply=NULL; [activeContext invalidate]; activeContext=nil; RemoveView(); });
}

static void BeginAuthentication(int32_t request, TouchIdReply reply, BOOL preferEmbedded) {
    generation++;
    [activeContext invalidate]; RemoveView();
    LAContext *context=[LAContext new]; activeContext=context;
    activeRequest=request; activeReply=reply;
    uint64_t token=generation;
    BOOL embedded=NO;
    NSError *error=nil;
    if (@available(macOS 12.0, *)) {
        if (preferEmbedded && [context canEvaluatePolicy:LAPolicyDeviceOwnerAuthenticationWithBiometrics error:&error]
            && context.biometryType==LABiometryTypeTouchID) {
            authenticationView=[[LAAuthenticationView alloc] initWithContext:context controlSize:NSControlSizeSmall];
            authenticationView.appearance=[NSAppearance appearanceNamed:NSAppearanceNameDarkAqua];
            PlaceView();
            [authenticationView.window.contentView layoutSubtreeIfNeeded];
            [authenticationView layoutSubtreeIfNeeded];
            embedded=authenticationView.window!=nil;
            if (!embedded) RemoveView();
        }
    }
    // Touch ID は必ず埋め込み表示を使う。配置に失敗しても、中央の認証画面へは戻さない。
    if (preferEmbedded && !embedded) {
        activeContext=nil; activeReply=NULL; RemoveView(); reply(request,4); return;
    }
    LAPolicy policy=embedded?LAPolicyDeviceOwnerAuthenticationWithBiometrics:LAPolicyDeviceOwnerAuthentication;
    context.localizedCancelTitle=@"キャンセル";
    context.localizedFallbackTitle=embedded?@"":@"パスワードを入力";
    context.touchIDAuthenticationAllowableReuseDuration=0;
    if (![context canEvaluatePolicy:policy error:&error]) {
        activeContext=nil; activeReply=NULL; RemoveView(); reply(request,2); return;
    }
    // NSView を追加した同じターンではなく、表示と埋め込み認証の登録後に開始する。
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW,80*NSEC_PER_MSEC),dispatch_get_main_queue(), ^{
        if (generation!=token) return;
        if (embedded && (!authenticationView.window.isVisible || NSIsEmptyRect(authenticationView.visibleRect))) {
            activeContext=nil; activeReply=NULL; RemoveView(); reply(request,4); return;
        }
        [context evaluatePolicy:policy localizedReason:@"ゲーム内のデスクトップのロックを解除します。"
                     reply:^(BOOL success,NSError *authError) {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (generation!=token) return;
            activeContext=nil; activeReply=NULL; RemoveView();
            int32_t result=success?0:3;
            if (!success && (authError.code==LAErrorUserCancel || authError.code==LAErrorSystemCancel || authError.code==LAErrorAppCancel)) result=1;
            if (!success && (authError.code==LAErrorBiometryLockout || authError.code==LAErrorBiometryNotAvailable || authError.code==LAErrorBiometryNotEnrolled)) result=2;
            reply(request,result);
        });
        }];
    });
}

__attribute__((visibility("default"))) void Team1TouchIdBegin(int32_t request, TouchIdReply reply, int32_t preferEmbedded) {
    OnMain(^{ BeginAuthentication(request,reply,preferEmbedded!=0); });
}

// パスワードを選んだときだけ、macOS 標準の認証画面へ切り替える。
__attribute__((visibility("default"))) void Team1TouchIdUsePassword(void) {
    OnMain(^{ if (activeReply) BeginAuthentication(activeRequest,activeReply,NO); });
}
