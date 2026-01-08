/**
 * Dashboard Hub Connection Manager
 * Manages the SignalR connection to the DashboardHub for real-time updates.
 */
const DashboardHub = (function() {
    'use strict';

    let connection = null;
    let isConnected = false;
    let reconnectAttempts = 0;
    const maxReconnectAttempts = 5;
    const reconnectDelayMs = 2000;

    // Event handlers storage
    const eventHandlers = {};

    /**
     * Initializes the SignalR connection to the dashboard hub.
     * @returns {Promise<boolean>} True if connection successful, false otherwise.
     */
    async function connect() {
        if (connection && isConnected) {
            console.log('[DashboardHub] Already connected');
            return true;
        }

        try {
            connection = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/dashboard')
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: (retryContext) => {
                        // Exponential backoff: 0s, 2s, 4s, 8s, 16s, then stop
                        if (retryContext.previousRetryCount >= maxReconnectAttempts) {
                            return null; // Stop reconnecting
                        }
                        return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 16000);
                    }
                })
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Set up connection state handlers
            connection.onreconnecting((error) => {
                console.warn('[DashboardHub] Connection lost, attempting to reconnect...', error);
                isConnected = false;
                triggerEvent('reconnecting', { error });
            });

            connection.onreconnected((connectionId) => {
                console.log('[DashboardHub] Reconnected with ID:', connectionId);
                isConnected = true;
                reconnectAttempts = 0;
                triggerEvent('reconnected', { connectionId });
            });

            connection.onclose((error) => {
                console.warn('[DashboardHub] Connection closed', error);
                isConnected = false;
                triggerEvent('disconnected', { error });
            });

            // Start the connection
            await connection.start();
            isConnected = true;
            reconnectAttempts = 0;
            console.log('[DashboardHub] Connected successfully');
            triggerEvent('connected', { connectionId: connection.connectionId });

            return true;
        } catch (error) {
            console.error('[DashboardHub] Failed to connect:', error);
            isConnected = false;
            triggerEvent('connectionFailed', { error });
            return false;
        }
    }

    /**
     * Disconnects from the dashboard hub.
     * @returns {Promise<void>}
     */
    async function disconnect() {
        if (connection) {
            try {
                await connection.stop();
                console.log('[DashboardHub] Disconnected');
            } catch (error) {
                console.error('[DashboardHub] Error during disconnect:', error);
            }
            isConnected = false;
        }
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
        on,
        off,
        isConnected: getIsConnected,
        connectionId: getConnectionId
    };
})();

// Auto-export for module systems if available
if (typeof module !== 'undefined' && module.exports) {
    module.exports = DashboardHub;
}
