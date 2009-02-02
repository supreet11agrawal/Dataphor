<?xml version="1.0" encoding="utf-8" ?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- -->
	<!--<xsl:output method="html" indent="no" />-->
	<!-- -->
    <!--
	<xsl:include href="common.xslt" />
	<xsl:include href="memberscommon.xslt" />
    -->
    <xsl:include href="member.xslt" />
    <xsl:include href="field.xslt" />
    <xsl:include href="property.xslt" />
    <xsl:include href="event.xslt" />
    <xsl:include href="memberoverload.xslt" />
	<!-- -->
    
    <!-- from memberscommon -->
	<!-- -->
	<xsl:template match="class | structure | interface" mode="process-individuals" >
        <!-- todo: make sure this is exhaustive -->
        <xsl:if test="count(./constructor) > 0">
            <xsl:choose>
               <xsl:when test="count(./constructor) > 1">
                   <xsl:apply-templates select="./constructor[1]" mode="overload">
                       <xsl:sort select="@name" />
                   </xsl:apply-templates>
               </xsl:when>
               <xsl:otherwise>
                    <xsl:call-template name="type-individuals">
                        <xsl:with-param name="member-type">constructor</xsl:with-param>
                    </xsl:call-template>
               </xsl:otherwise>
            </xsl:choose>
        </xsl:if>
        <xsl:if test="count(./field) > 0">
            <xsl:comment>generated by IndividualMembers.xslt field</xsl:comment>
            <xsl:call-template name="type-individuals">
                <xsl:with-param name="member-type">field</xsl:with-param>
            </xsl:call-template>
        </xsl:if>
        <xsl:if test="count(./property) > 0">
            <xsl:call-template name="type-individuals">
                <xsl:with-param name="member-type">property</xsl:with-param>
            </xsl:call-template>
            <!-- handle the overloads, if any -->
            <xsl:for-each select="./property">
                <xsl:sort select="@name" />
                <xsl:if test="@overload=1"> <!-- is this sufficient? -->
                   <xsl:apply-templates select="." mode="overload">
                       <xsl:sort select="@name" />
                   </xsl:apply-templates>
                </xsl:if>
            </xsl:for-each>
        </xsl:if>
        <xsl:if test="count(./method) > 0">
            <xsl:call-template name="type-individuals">
                <xsl:with-param name="member-type">method</xsl:with-param>
            </xsl:call-template>
            <!-- handle the overloads, if any -->
            <xsl:for-each select="./method">
                <xsl:sort select="@name" />
                <xsl:if test="@overload=1"> <!-- is this sufficient? -->
                   <xsl:apply-templates select="." mode="overload">
                       <xsl:sort select="@name" />
                   </xsl:apply-templates>
                </xsl:if>
            </xsl:for-each>
        </xsl:if>
        <xsl:if test="count(./event) > 0">
            <xsl:comment>generated by IndividualMembers.xslt event</xsl:comment>
            <xsl:call-template name="type-individuals">
                <xsl:with-param name="member-type">event</xsl:with-param>
            </xsl:call-template>
            <!-- handle the overloads, if any -->
            <xsl:for-each select="./event">
                <xsl:sort select="@name" />
                <xsl:if test="@overload=1"> <!-- is this sufficient? -->
                   <xsl:apply-templates select="." mode="overload">
                       <xsl:sort select="@name" />
                   </xsl:apply-templates>
                </xsl:if>
            </xsl:for-each>
        </xsl:if>
        <xsl:if test="count(./operator) > 0">
            <xsl:comment>generated by IndividualMembers.xslt operator</xsl:comment>
            <xsl:call-template name="type-individuals">
                <xsl:with-param name="member-type">operator</xsl:with-param>
            </xsl:call-template>
            <!-- handle the overloads, if any -->
            <xsl:for-each select="./operator">
                <xsl:sort select="@name" />
                <xsl:if test="@overload=1"> <!-- is this sufficient? -->
                   <xsl:apply-templates select="." mode="overload">
                       <xsl:sort select="@name" />
                   </xsl:apply-templates>
                </xsl:if>
            </xsl:for-each>
        </xsl:if>
        <!--verify this doesn't create duplicates -->
        <xsl:if test="count(./delegate) > 0">
            <xsl:comment>generated by IndividualMembers.xslt delegate</xsl:comment>
            <xsl:call-template name="type-individuals">
                <xsl:with-param name="member-type">delegate</xsl:with-param>
            </xsl:call-template>
            <!-- handle the overloads, if any -->
            <xsl:for-each select="./delegate">
                <xsl:sort select="@name" />
                <xsl:if test="@overload=1"> <!-- is this sufficient? -->
                   <xsl:apply-templates select="." mode="overload">
                       <xsl:sort select="@name" />
                   </xsl:apply-templates>
                </xsl:if>
            </xsl:for-each>
        </xsl:if>
	</xsl:template>
    <!--
	<xsl:param name='id' />
	<xsl:param name='member-type' />
    -->
    <!-- -->
	<xsl:template name="type-individuals">
		<xsl:param name="member-type" />
        <xsl:param name="name" />
		<xsl:variable name="Members">
			<xsl:call-template name="get-big-member-plural">
				<xsl:with-param name="member" select="$member-type" />
			</xsl:call-template>
		</xsl:variable>
		<xsl:variable name="members">
			<xsl:call-template name="get-small-member-plural">
				<xsl:with-param name="member" select="$member-type" />
			</xsl:call-template>
		</xsl:variable>
        <xsl:variable name="filename" >
            <xsl:call-template name="get-filename-for-type">
                <xsl:with-param name="id" select="@id"/>
            </xsl:call-template>
        </xsl:variable>
        <sect3>
            <xsl:attribute name="id">
                <xsl:value-of select="concat(substring-before($filename,'.html'),$Members)"/>
            </xsl:attribute>
            <xsl:comment>Generated from individualmembers.xsl for <xsl:value-of select="$member-type"/></xsl:comment>
            <title><xsl:value-of select="concat(@name, ' ', $Members)" /></title>
            <para>
                <xsl:text>The </xsl:text>
                <xsl:value-of select="$members" />
                <xsl:text> of the </xsl:text>
                <emphasis role="bold">
                    <xsl:value-of select="@name" />
                </emphasis>
                <xsl:text> class are listed below. For a complete list of </xsl:text>
                <classname>
                    <xsl:value-of select="@name" />
                </classname>
                <xsl:text> class members, see the </xsl:text>
                <ulink>
                    <xsl:attribute name="url">
                        <xsl:call-template name="get-filename-for-type-members">
                            <xsl:with-param name="id" select="@id" />
                        </xsl:call-template>
                    </xsl:attribute>
                    <xsl:value-of select="@name" />
                    <xsl:text> Members</xsl:text>
                </ulink>
                <xsl:text> topic.</xsl:text>
            </para>
            <!-- static members -->
            <xsl:call-template name="public-static-section">
                <xsl:with-param name="member" select="$member-type" />
            </xsl:call-template>
            <xsl:call-template name="protected-static-section">
                <xsl:with-param name="member" select="$member-type" />
            </xsl:call-template>
            <xsl:call-template name="protected-internal-static-section">
                <xsl:with-param name="member" select="$member-type" />
            </xsl:call-template>
            <xsl:call-template name="internal-static-section">
                <xsl:with-param name="member" select="$member-type" />
            </xsl:call-template>
            <xsl:call-template name="private-static-section">
                <xsl:with-param name="member" select="$member-type" />
            </xsl:call-template>
            <!-- instance members -->
            <xsl:call-template name="public-instance-section">
                <xsl:with-param name="member" select="$member-type" />
            </xsl:call-template>
            <xsl:call-template name="protected-instance-section">
                <xsl:with-param name="member" select="$member-type" />
            </xsl:call-template>
            <xsl:call-template name="protected-internal-instance-section">
                <xsl:with-param name="member" select="$member-type" />
            </xsl:call-template>
            <xsl:call-template name="internal-instance-section">
                <xsl:with-param name="member" select="$member-type" />
            </xsl:call-template>
            <xsl:call-template name="private-instance-section">
                <xsl:with-param name="member" select="$member-type" />
            </xsl:call-template>
            <xsl:call-template name="explicit-interface-implementations">
                <xsl:with-param name="member" select="$member-type" />
            </xsl:call-template>
            <xsl:call-template name="seealso-section">
                <xsl:with-param name="page">
                    <xsl:value-of select="$members" />
                </xsl:with-param>
            </xsl:call-template>
            <xsl:comment>This is where the individual members should be processed.</xsl:comment>
            <!-- process all the members -->
            <xsl:call-template name="DoMemberPages">
                <xsl:with-param name="member" select="$member-type"/>
            </xsl:call-template>
        </sect3>
	</xsl:template>
	<!-- -->
    <xsl:template name="DoMemberPages">
        <xsl:param name="member"/>
        <xsl:comment>DoMemberPages processing: <xsl:value-of select="$member"/></xsl:comment>
        <xsl:choose>
            <xsl:when test="$member='constructor'">
                <!-- todo: see if this overduplicates the work -->
               <xsl:apply-templates select="./constructor" mode="singleton">
                   <xsl:sort select="@name" />
               </xsl:apply-templates>
            </xsl:when>
            <xsl:when test="$member='method'">
               <xsl:apply-templates select="./method[not(@overload)]" mode="singleton">
                   <xsl:sort select="@name" />
               </xsl:apply-templates>
            </xsl:when>
            <xsl:when test="$member='field'">
               <xsl:apply-templates select="./field[not(@overload)]" mode="singleton">
                   <xsl:sort select="@name" />
               </xsl:apply-templates>
            </xsl:when>
            <xsl:when test="$member='property'">
               <xsl:apply-templates select="./property[not(@overload)]" mode="singleton">
                   <xsl:sort select="@name" />
               </xsl:apply-templates>
            </xsl:when>
            <xsl:when test="$member='event'">
               <xsl:apply-templates select="./event" mode="singleton">
                   <xsl:sort select="@name" />
               </xsl:apply-templates>
            </xsl:when>
            <xsl:when test="$member='operator'">
               <xsl:apply-templates select="./operator[not(@overload)]" mode="singleton">
                   <xsl:sort select="@name" />
               </xsl:apply-templates>
            </xsl:when>
            <!--verify this doesn't create duplicates -->
            <xsl:when test="$member='delegate'">
               <xsl:apply-templates select="./delegate[not(@overload)]" mode="singleton">
                   <xsl:sort select="@name" />
               </xsl:apply-templates>
            </xsl:when>
        </xsl:choose>
    </xsl:template>
</xsl:stylesheet>
