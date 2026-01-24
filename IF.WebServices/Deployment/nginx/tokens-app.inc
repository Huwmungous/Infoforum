# Tokens App (display-token-app) nginx configuration
# Include this in your main server block: include /etc/nginx/conf.d/tokens-app.inc;

# Tokens App - React SPA with realm/client in URL path
# URL pattern: /tokens/{realm}/{client}/...
location /tokens {
    alias /var/www/tokens;
    
    # Try static files first, fall back to index.html for SPA routing
    try_files $uri $uri/ /tokens/index.html;
    
    # Cache static assets
    location ~* /tokens/assets/.*\.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
        alias /var/www/tokens;
        try_files $uri =404;
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
    
    # Don't cache index.html (allows updates to take effect immediately)
    location = /tokens/index.html {
        alias /var/www/tokens/index.html;
        expires -1;
        add_header Cache-Control "no-store, no-cache, must-revalidate";
    }
}
