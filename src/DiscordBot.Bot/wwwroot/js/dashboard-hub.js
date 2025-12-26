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
        connect,
        disconnect,
        joinGuildGroup,
        leaveGuildGroup,
        getCurrentStatus,
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
