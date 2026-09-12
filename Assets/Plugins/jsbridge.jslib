mergeInto(LibraryManager.library, {
  /**
   * Runs an asynchronous task in JavaScript and passes the result or error
   * back to a WebAssembly function.
   *
   * @param {number} $taskName - Pointer to a UTF-8 encoded task name.
   * @param {number} $taskId - Pointer to a UTF-8 encoded unique task ID.
   * @param {number} $args - Pointer to a UTF-8 encoded string of arguments.
   * @param {number} cb - Pointer to the WebAssembly callback function.
   */
  RunAsyncTask: function ($taskName, $taskId, $args, cb) {
    /**
     * Converts a JavaScript string to a UTF-8 encoded memory buffer
     * and returns a pointer to it.
     *
     * @param {string} str - The string to convert.
     * @returns {number} Pointer to allocated memory, or null if input is empty.
     */
    function getPtrFromString(str) {
      if (str) {
        var size = lengthBytesUTF8(str) + 1; // +1 for null terminator
        var buffer = _malloc(size); // Allocate memory in WebAssembly heap
        stringToUTF8(str, buffer, size); // Write UTF-8 encoded string
        return buffer;
      } else {
        return null;
      }
    }

    var taskId = UTF8ToString($taskId); // Convert taskId from pointer to string

    // Execute the asynchronous task and handle success/error cases
    asyncTask(UTF8ToString($taskName), UTF8ToString($args))
      .then(function (result) {
        console.log('RunAsyncTask got result', result);
        // Call the WebAssembly callback with task ID and result (success case)
        wasmTable.get(cb)(getPtrFromString(taskId), getPtrFromString(result), null);
      })
      .catch(function (error) {
        console.log('RunAsyncTask got failure', error);
        // Call the WebAssembly callback with task ID and error message (failure case)
        wasmTable.get(cb)(getPtrFromString(taskId), null, getPtrFromString(error));
      });
  },

  /**
   * Runs a synchronous task and returns the result as a pointer to a UTF-8 string.
   *
   * @param {number} $taskName - Pointer to a UTF-8 encoded task name.
   * @param {number} $args - Pointer to a UTF-8 encoded string of arguments.
   * @returns {number} Pointer to the allocated UTF-8 result string.
   */
  RunSyncTask: function ($taskName, $args) {
    /**
     * Converts a JavaScript string to a UTF-8 encoded memory buffer
     * and returns a pointer to it.
     *
     * @param {string} str - The string to convert.
     * @returns {number} Pointer to allocated memory, or null if input is empty.
     */
    function getPtrFromString(str) {
      if (str) {
        var size = lengthBytesUTF8(str) + 1;
        var buffer = _malloc(size);
        stringToUTF8(str, buffer, size);
        return buffer;
      } else {
        return null;
      }
    }

    // Convert the task name and arguments from pointers to JavaScript strings
    var response = syncTask(UTF8ToString($taskName), UTF8ToString($args));

    // Convert the response to a WebAssembly pointer and return it
    return getPtrFromString(response);
  },
});
