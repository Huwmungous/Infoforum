#!/bin/bash
# setup-sfd-env.sh
# Sets up SfD environment variables system-wide on Fedora
# Run as root: sudo ./setup-sfd-env.sh

set -e

# Colours for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Colour

echo -e "${GREEN}Setting up SfD environment variables system-wide...${NC}"

# Method 1: /etc/environment (read by PAM, works for all sessions)
if ! grep -q "SFD_CLIENT" /etc/environment 2>/dev/null; then
    cat >> /etc/environment << 'EOF'

# SfD Service Environment Variables
SFD_CLIENT=dev-login
SFD_CLIENTSECRET=SppIDs3eJ3M6lZ6KEmKFn3gxXqJXEa58
SFD_CONFIG_SERVICE=https://sfddevelopment.com/config
SFD_REALM=SfdDevelopment_Dev
EOF
    echo -e "${GREEN}✓ Added to /etc/environment${NC}"
else
    echo -e "${YELLOW}○ /etc/environment already contains SFD variables${NC}"
fi

# Method 2: /etc/profile.d/ (for shell sessions)
cat > /etc/profile.d/sfd.sh << 'EOF'
# SfD Service Environment Variables
export SFD_CLIENT=dev-login
export SFD_CLIENTSECRET=SppIDs3eJ3M6lZ6KEmKFn3gxXqJXEa58
export SFD_CONFIG_SERVICE=https://sfddevelopment.com/config
export SFD_REALM=SfdDevelopment_Dev
EOF

chmod 644 /etc/profile.d/sfd.sh
echo -e "${GREEN}✓ Created /etc/profile.d/sfd.sh${NC}"

# Method 3: systemd system-wide environment (ensures all services get these)
mkdir -p /etc/systemd/system.conf.d
cat > /etc/systemd/system.conf.d/sfd-env.conf << 'EOF'
[Manager]
DefaultEnvironment=SFD_CLIENT=dev-login SFD_CLIENTSECRET=SppIDs3eJ3M6lZ6KEmKFn3gxXqJXEa58 SFD_CONFIG_SERVICE=https://sfddevelopment.com/config SFD_REALM=SfdDevelopment_Dev
EOF

echo -e "${GREEN}✓ Created /etc/systemd/system.conf.d/sfd-env.conf${NC}"

# Reload systemd manager to pick up new defaults
systemctl daemon-reexec

echo -e "${GREEN}Done!${NC}"
echo ""
echo "Environment variables set system-wide via:"
echo "  /etc/environment"
echo "  /etc/profile.d/sfd.sh"
echo "  /etc/systemd/system.conf.d/sfd-env.conf"
echo ""
echo -e "${YELLOW}IMPORTANT: Update SFD_CLIENTSECRET with the correct secret for dev-login-svc${NC}"
echo ""
echo -e "${YELLOW}Reboot recommended, or restart services:${NC}"
echo "  sudo systemctl restart configwebservice loggerwebservice"
