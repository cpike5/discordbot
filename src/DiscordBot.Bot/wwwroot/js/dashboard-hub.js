/**
 * Dashboard Hub Connection Manager
 * Manages the SignalR connection to the DashboardHub for real-time updates.
 *
 * Connection states (see getConnectionState / onStateChange): 'connecting' (first attempt),
 * 'connected', 'reconnecting' (lost, and still trying, which includes a first attempt that failed),
 * 'disconnected' (only after disconnect() is called). The connection never gives up: see
 * nextRetryDelay. connection-banner.js turns these states into the page-wide banner.
 */
const DashboardHub = (function() {
    'use strict';

    // Reconnect policy: a few fast, visible retries, then a steady pace forever. A dashboard is left
    // open for hours and the server restarts on every deploy, so giving up (as this used to after
    // five tries) strands the page on stale data until someone reloads it.
    const FAST_RETRY_DELAYS_MS = [0, 2000, 5000, 10000];
    const SLOW_RETRY_MS = 30000;
    const SLOW_RETRY_JITTER_MS = 10000; // spread over 25-35s so a restart is not a thundering herd

    /**
     * How long to wait before reconnect attempt number `previousRetryCount` (0-based). Never null:
     * returning null is how SignalR is told to stop.
     * @param {number} previousRetryCount - Attempts already made in this outage.
     * @param {function} [random] - Injectable for tests; defaults to Math.random.
     * @returns {number} Milliseconds.
     */
    function nextRetryDelay(previousRetryCount, random) {
        if (previousRetryCount < FAST_RETRY_DELAYS_MS.length) {
            return FAST_RETRY_DELAYS_MS[previousRetryCount];
        }
        const r = (random || Math.random)();
        return SLOW_RETRY_MS - SLOW_RETRY_JITTER_MS / 2 + Math.round(r * SLOW_RETRY_JITTER_MS);
    }

    let connection = null;
    let isConnected = false;
    let pendingConnect = null;

    // Manual retry loop, for when start() itself fails: SignalR's automatic reconnect only covers
    // a connection that was already up, so a page opened while the server is down needs this.
    let retryTimer = null;
    let retryCount = 0;
    let recovering = false;   // an attempt has failed since the last time we were connected
    let stopped = false;      // disconnect() was called: do not retry
    let restarting = false;   // retryNow() is replacing a stuck automatic reconnect

    // Event handlers storage
    const eventHandlers = {};

    // Connection state management
    let connectionState = 'disconnected';
    let stateChangeCallbacks = [];

    /**
     * Updates the connection state and notifies all state change subscribers.
     * @param {string} newState - 'disconnected', 'connecting', 'connected' or 'reconnecting'.
     */
    function setConnectionState(newState) {
        const previousState = connectionState;
        if (newState === previousState) return;
        connectionState = newState;
        stateChangeCallbacks.forEach(callback => {
            try {
                callback({ state: newState, previousState });
            } catch (error) {
                console.error('[DashboardHub] Error in state change callback:', error);
            }
        });
    }

    function clearRetryTimer() {
        if (retryTimer !== null) {
            clearTimeout(retryTimer);
            retryTimer = null;
        }
    }

    function scheduleRetry() {
        if (stopped || retryTimer !== null) return;
        const delay = nextRetryDelay(retryCount++);
        retryTimer = setTimeout(() => {
            retryTimer = null;
            attempt();
        }, delay);
    }

    /**
     * Initializes the SignalR connection to the dashboard hub.
     * @returns {Promise<boolean>} True if connection successful, false otherwise. A false result
     * is not final: the hub keeps retrying in the background and raises 'connected' and
     * 'reconnected' when it gets through.
     */
    async function connect() {
        if (connection && isConnected) {
            console.log('[DashboardHub] Already connected');
            return true;
        }
        // The layout and page scripts both call connect() on load. Share the attempt in flight;
        // starting a second one would replace `connection` while it is still connecting, and
        // invokes on it fail with "not in the 'Connected' State".
        return attempt();
    }

    function attempt() {
        if (pendingConnect) {
            return pendingConnect;
        }
        // SignalR is busy reconnecting by itself; a second start() would throw.
        if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
            return Promise.resolve(isConnected);
        }
        pendingConnect = startConnection().finally(() => { pendingConnect = null; });
        return pendingConnect;
    }

    function buildConnection() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/dashboard')
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: (retryContext) => nextRetryDelay(retryContext.previousRetryCount)
            })
            .configureLogging(signalR.LogLevel.Information)
            .build();

        connection.onreconnecting((error) => {
            console.warn('[DashboardHub] Connection lost, attempting to reconnect...', error);
            isConnected = false;
            setConnectionState('reconnecting');
            triggerEvent('reconnecting', { error });
        });

        connection.onreconnected((connectionId) => {
            console.log('[DashboardHub] Reconnected with ID:', connectionId);
            isConnected = true;
            retryCount = 0;
            recovering = false;
            setConnectionState('connected');
            triggerEvent('reconnected', { connectionId });
        });

        connection.onclose((error) => {
            isConnected = false;
            if (stopped || restarting) {
                console.log('[DashboardHub] Connection closed');
                if (!restarting) {
                    setConnectionState('disconnected');
                    triggerEvent('disconnected', { error });
                }
                return;
            }
            // Automatic reconnect never gives up, so this is the server closing the connection
            // for good. Carry on with our own retries.
            console.warn('[DashboardHub] Connection closed by the server, retrying', error);
            recovering = true;
            setConnectionState('reconnecting');
            triggerEvent('disconnected', { error });
            scheduleRetry();
        });

        // Register every handler stored so far. From here on, on() registers directly, so a handler
        // is never attached twice.
        for (const [eventName, handlers] of Object.entries(eventHandlers)) {
            for (const handler of handlers) {
                connection.on(eventName, handler);
            }
        }
    }

    async function startConnection() {
        stopped = false;
        clearRetryTimer();
        if (!connection) {
            buildConnection();
        }

        setConnectionState(recovering ? 'reconnecting' : 'connecting');
        try {
            await connection.start();
        } catch (error) {
            console.error('[DashboardHub] Failed to connect:', error);
            isConnected = false;
            const firstFailure = !recovering;
            recovering = true;
            setConnectionState('reconnecting');
            if (firstFailure) {
                triggerEvent('connectionFailed', { error });
            }
            scheduleRetry();
            return false;
        }

        isConnected = true;
        retryCount = 0;
        const wasRecovering = recovering;
        recovering = false;
        setConnectionState('connected');

        console.log('[DashboardHub] Connected successfully');
        triggerEvent('connected', { connectionId: connection.connectionId });
        if (wasRecovering) {
            // Pages rejoin their groups on 'reconnected'; group membership does not survive a new connection.
            triggerEvent('reconnected', { connectionId: connection.connectionId });
        }
        return true;
    }

    /**
     * Tries to reconnect now instead of waiting out the retry delay. Used by the banner's "Retry
     * now" button and when the browser comes back online or the tab becomes visible again.
     * @returns {Promise<boolean>} True if connected afterwards.
     */
    async function retryNow() {
        if (isConnected || !connection || stopped || pendingConnect) {
            return isConnected;
        }
        if (connection.state === signalR.HubConnectionState.Reconnecting) {
            // SignalR is waiting out a delay we cannot shorten; end that wait and start over.
            restarting = true;
            try {
                await connection.stop();
            } catch (error) {
                console.warn('[DashboardHub] Error while restarting the connection:', error);
            } finally {
                restarting = false;
            }
            recovering = true;
        }
        retryCount = 0;
        return attempt();
    }

    /**
     * Disconnects from the dashboard hub.
     * @returns {Promise<void>}
     */
    async function disconnect() {
        stopped = true;
        clearRetryTimer();
        if (connection) {
            try {
                await connection.stop();
                console.log('[DashboardHub] Disconnected');
            } catch (error) {
                console.error('[DashboardHub] Error during disconnect:', error);
            }
            isConnected = false;
            setConnectionState('disconnected');
        }
    }

    // A laptop waking up or a phone leaving a tunnel should not wait out a 30 second delay.
    if (typeof window !== 'undefined' && typeof document !== 'undefined') {
        window.addEventListener('online', () => { retryNow(); });
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) retryNow();
        });
    }


    /**
     * Joins a guild-specific group to receive updates for that guild.
     * @param {string} guildId - The Discord guild ID.
     * @returns {Promise<void>}
     */
    async function joinGuildGroup(guildId) {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot join guild group');
            return;
        }

        try {
            await connection.invoke('JoinGuildGroup', guildId);
            console.log('[DashboardHub] Joined guild group:', guildId);
        } catch (error) {
            console.error('[DashboardHub] Failed to join guild group:', error);
        }
    }

    /**
     * Leaves a guild-specific group.
     * @param {string} guildId - The Discord guild ID.
     * @returns {Promise<void>}
     */
    async function leaveGuildGroup(guildId) {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot leave guild group');
            return;
        }

        try {
            await connection.invoke('LeaveGuildGroup', guildId);
            console.log('[DashboardHub] Left guild group:', guildId);
        } catch (error) {
            console.error('[DashboardHub] Failed to leave guild group:', error);
        }
    }

    /**
     * Gets the current bot status from the server.
     * @returns {Promise<object|null>} The bot status object or null on error.
     */
    async function getCurrentStatus() {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot get status');
            return null;
        }

        try {
            const status = await connection.invoke('GetCurrentStatus');
            return status;
        } catch (error) {
            console.error('[DashboardHub] Failed to get status:', error);
            return null;
        }
    }

    /**
     * Joins the performance group to receive real-time performance metrics updates.
     * @returns {Promise<void>}
     */
    async function joinPerformanceGroup() {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot join performance group');
            return;
        }
        try {
            await connection.invoke('JoinPerformanceGroup');
            console.log('[DashboardHub] Joined performance group');
        } catch (error) {
            console.error('[DashboardHub] Failed to join performance group:', error);
        }
    }

    /**
     * Leaves the performance group to stop receiving real-time performance metrics updates.
     * @returns {Promise<void>}
     */
    async function leavePerformanceGroup() {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot leave performance group');
            return;
        }
        try {
            await connection.invoke('LeavePerformanceGroup');
            console.log('[DashboardHub] Left performance group');
        } catch (error) {
            console.error('[DashboardHub] Failed to leave performance group:', error);
        }
    }

    /**
     * Gets the current performance metrics from the server.
     * @returns {Promise<object|null>} The performance metrics object or null on error.
     */
    async function getCurrentPerformanceMetrics() {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot get performance metrics');
            return null;
        }
        try {
            return await connection.invoke('GetCurrentPerformanceMetrics');
        } catch (error) {
            console.error('[DashboardHub] Failed to get performance metrics:', error);
            return null;
        }
    }

    /**
     * Joins the alerts group to receive real-time alert notifications.
     * @returns {Promise<void>}
     */
    async function joinAlertsGroup() {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot join alerts group');
            return;
        }
        try {
            await connection.invoke('JoinAlertsGroup');
            console.log('[DashboardHub] Joined alerts group');
        } catch (error) {
            console.error('[DashboardHub] Failed to join alerts group:', error);
        }
    }

    /**
     * Leaves the alerts group to stop receiving alert notifications.
     * @returns {Promise<void>}
     */
    async function leaveAlertsGroup() {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot leave alerts group');
            return;
        }
        try {
            await connection.invoke('LeaveAlertsGroup');
            console.log('[DashboardHub] Left alerts group');
        } catch (error) {
            console.error('[DashboardHub] Failed to leave alerts group:', error);
        }
    }

    /**
     * Gets the current active alert count from the server.
     * @returns {Promise<object|null>} The active alert summary or null on error.
     */
    async function getActiveAlertCount() {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot get active alert count');
            return null;
        }
        try {
            return await connection.invoke('GetActiveAlertCount');
        } catch (error) {
            console.error('[DashboardHub] Failed to get active alert count:', error);
            return null;
        }
    }

    /**
     * Joins the system health group to receive real-time system health updates.
     * @returns {Promise<void>}
     */
    async function joinSystemHealthGroup() {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot join system health group');
            return;
        }
        try {
            await connection.invoke('JoinSystemHealthGroup');
            console.log('[DashboardHub] Joined system health group');
        } catch (error) {
            console.error('[DashboardHub] Failed to join system health group:', error);
        }
    }

    /**
     * Leaves the system health group to stop receiving system health updates.
     * @returns {Promise<void>}
     */
    async function leaveSystemHealthGroup() {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot leave system health group');
            return;
        }
        try {
            await connection.invoke('LeaveSystemHealthGroup');
            console.log('[DashboardHub] Left system health group');
        } catch (error) {
            console.error('[DashboardHub] Failed to leave system health group:', error);
        }
    }

    /**
     * Gets the current system health metrics from the server.
     * @returns {Promise<object|null>} The system health metrics object or null on error.
     */
    async function getCurrentSystemHealth() {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot get system health');
            return null;
        }
        try {
            return await connection.invoke('GetCurrentSystemHealth');
        } catch (error) {
            console.error('[DashboardHub] Failed to get system health:', error);
            return null;
        }
    }

    /**
     * Joins a guild-specific audio group to receive audio events for that guild.
     * @param {string} guildId - The Discord guild ID.
     * @returns {Promise<void>}
     */
    async function joinGuildAudioGroup(guildId) {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot join guild audio group');
            return;
        }
        try {
            await connection.invoke('JoinGuildAudioGroup', guildId);
            console.log('[DashboardHub] Joined guild audio group:', guildId);
        } catch (error) {
            console.error('[DashboardHub] Failed to join guild audio group:', error);
        }
    }

    /**
     * Leaves a guild-specific audio group.
     * @param {string} guildId - The Discord guild ID.
     * @returns {Promise<void>}
     */
    async function leaveGuildAudioGroup(guildId) {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot leave guild audio group');
            return;
        }
        try {
            await connection.invoke('LeaveGuildAudioGroup', guildId);
            console.log('[DashboardHub] Left guild audio group:', guildId);
        } catch (error) {
            console.error('[DashboardHub] Failed to leave guild audio group:', error);
        }
    }

    /**
     * Gets the current audio status for a guild.
     * @param {string} guildId - The Discord guild ID.
     * @returns {Promise<object|null>} The audio status object or null on error.
     */
    async function getCurrentAudioStatus(guildId) {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot get audio status');
            return null;
        }
        try {
            return await connection.invoke('GetCurrentAudioStatus', guildId);
        } catch (error) {
            console.error('[DashboardHub] Failed to get audio status:', error);
            return null;
        }
    }

    /**
     * Registers a handler for a specific server event.
     * @param {string} eventName - The event name from the server.
     * @param {function} handler - The callback function.
     */
    function on(eventName, handler) {
        if (!eventHandlers[eventName]) {
            eventHandlers[eventName] = [];
        }
        eventHandlers[eventName].push(handler);

        // Register with SignalR if connected
        if (connection) {
            connection.on(eventName, handler);
        }
    }

    /**
     * Removes a handler for a specific server event.
     * @param {string} eventName - The event name.
     * @param {function} handler - The handler to remove.
     */
    function off(eventName, handler) {
        if (eventHandlers[eventName]) {
            const index = eventHandlers[eventName].indexOf(handler);
            if (index > -1) {
                eventHandlers[eventName].splice(index, 1);
            }
        }

        if (connection) {
            connection.off(eventName, handler);
        }
    }

    /**
     * Triggers local event handlers (for connection state events).
     * @param {string} eventName - The event name.
     * @param {object} data - The event data.
     */
    function triggerEvent(eventName, data) {
        if (eventHandlers[eventName]) {
            eventHandlers[eventName].forEach(handler => {
                try {
                    handler(data);
                } catch (error) {
                    console.error('[DashboardHub] Error in event handler:', error);
                }
            });
        }
    }
    /**
     * Invokes a hub method with the specified arguments.
     * @param {string} methodName - The hub method name to invoke.
     * @param {...*} args - Arguments to pass to the hub method.
     * @returns {Promise<*>} The result from the hub method.
     */
    async function invoke(methodName, ...args) {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot invoke method:', methodName);
            return null;
        }

        try {
            return await connection.invoke(methodName, ...args);
        } catch (error) {
            console.error('[DashboardHub] Failed to invoke method:', methodName, error);
            return null;
        }
    }

    /**
     * Checks if currently connected.
     * @returns {boolean} True if connected.
     */
    function getIsConnected() {
        return isConnected;
    }

    /**
     * Gets the current connection ID.
     * @returns {string|null} The connection ID or null if not connected.
     */
    function getConnectionId() {
        return connection ? connection.connectionId : null;
    }

    // Public API
    return {
        invoke,
        connect,
        retryNow,
        nextRetryDelay,
        disconnect,
        joinGuildGroup,
        leaveGuildGroup,
        getCurrentStatus,
        joinPerformanceGroup,
        leavePerformanceGroup,
        getCurrentPerformanceMetrics,
        joinAlertsGroup,
        leaveAlertsGroup,
        getActiveAlertCount,
        joinSystemHealthGroup,
        leaveSystemHealthGroup,
        getCurrentSystemHealth,
        joinGuildAudioGroup,
        leaveGuildAudioGroup,
        getCurrentAudioStatus,
        on,
        off,
        isConnected: getIsConnected,
        connectionId: getConnectionId,
        getConnectionState: () => connectionState,
        onStateChange: (callback) => { stateChangeCallbacks.push(callback); },
        offStateChange: (callback) => { stateChangeCallbacks = stateChangeCallbacks.filter(cb => cb !== callback); }
    };
})();

// Auto-export for module systems if available
if (typeof module !== 'undefined' && module.exports) {
    module.exports = DashboardHub;
}
