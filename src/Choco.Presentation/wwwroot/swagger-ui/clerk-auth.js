(() => {
  const schemeName = 'Bearer';
  const scheme = { type: 'http', scheme: 'bearer', bearerFormat: 'JWT' };
  const tokenRefreshIntervalMs = 10000;
  let swagger;
  let clerkLoaded = false;
  let token = null;

  const isSameOrigin = (url) => new URL(url, window.location.href).origin === window.location.origin;

  const refreshToken = async () => {
    token = clerkLoaded ? (await window.Clerk.session?.getToken()) ?? null : null;
  };

  const syncAuthorization = async () => {
    await refreshToken();

    if (!swagger || !clerkLoaded) {
      return;
    }

    if (token) {
      swagger.authActions.authorize({ [schemeName]: { name: schemeName, schema: scheme, value: token } });
    } else if (swagger.authSelectors.authorized().has(schemeName)) {
      swagger.authActions.logout([schemeName]);
    }
  };

  window.attachClerkToken = (request) => {
    if (token && !request.loadSpec && isSameOrigin(request.url)) {
      request.headers.Authorization = `Bearer ${token}`;
    }

    return request;
  };

  window.ClerkAuthPlugin = () => ({
    afterLoad(system) {
      swagger = system;
      syncAuthorization();
    },
    statePlugins: {
      auth: {
        wrapActions: {
          showDefinitions: (original) => (definitions) => {
            if (definitions && clerkLoaded && !window.Clerk.session) {
              return window.Clerk.redirectToSignIn({ redirectUrl: window.location.href });
            }

            return original(definitions);
          },
          logout: (original) => (names) => {
            if (clerkLoaded && window.Clerk.session) {
              window.Clerk.signOut(() => {});
            }

            return original(names);
          },
        },
      },
    },
  });

  window.addEventListener('load', async () => {
    try {
      await window.Clerk.load();
      clerkLoaded = true;
      window.Clerk.addListener(syncAuthorization);
      window.setInterval(refreshToken, tokenRefreshIntervalMs);
      await syncAuthorization();
    } catch (error) {
      console.error('Clerk failed to load; the Authorize button falls back to a pasted token.', error);
    }
  });
})();
